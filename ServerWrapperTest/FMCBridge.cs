using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Net;
using Ini;
using SuccExceptions;
using Factorio_MC_Bridge;
using CoreRCON;
using CoreRCON.Parsers.Standard;

namespace ServerWrapperTest
{
    class FMCBridge
    {
		public static readonly Util.LogFormat OutputFormat = new Util.LogFormat("[FMCBridge] ", ConsoleColor.Magenta);
        public static readonly Util.LogFormat ErrorFormat = new Util.LogFormat("[FMCBridge Error] ", ConsoleColor.DarkMagenta);

        public static bool Running;

        public static string RootPath;

        public static void Run() {
			FactorioServer.Run(true);
			MinecraftServer.Run(true);

            Wrapper.WriteLine("Preparing FMCBridge...");

            string SettingsFileName = "Settings.ini";
            string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);
            IniFile s = new IniFile(SettingsFileName);
            RootPath = s.Read("ServerFolder", "Windows") + '\\';

            /*
				Load in the item mappings file.  
			*/
            DualDictionary<String, String> itemMappings = new DualDictionary<String, String>();
            Dictionary<String, double> minecraftRatios = new Dictionary<String, double>();
            Dictionary<String, double> factorioRatios = new Dictionary<String, double>();

            //Open up the file stream for the item mappings
            string itemMappingsPath = Path.Combine(RootPath, s.Read("MappingsFile", "FMCBridge"));
            FileStream fileStream = new FileStream(itemMappingsPath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read);
            StreamReader streamReader = new StreamReader(fileStream, Encoding.Default);

            //This loops needs to do a couple of things. 
            //The first is it needs to read in the mappings into the DualDictionary for better translation of names.
            //The second it needs to bind the item ratios to their respective lists.
            while (!streamReader.EndOfStream)
            {
                //Item Name Mappings first
                String readString = streamReader.ReadLine();
                if (readString.Contains("#") || readString.Equals("") || readString.Equals("\n"))
                {
                    continue;
                }
                String[] split = readString.Split('=');
                itemMappings.Add(split[0], split[1]);
                //Split the string again to get the ratios
                if (split.Length > 2)
                {
                    String[] ratios = split[2].Split(':');
                    minecraftRatios.Add(split[0], Double.Parse(ratios[0]));
                    factorioRatios.Add(split[1], Double.Parse(ratios[1]));
                }
            }
            streamReader.Close();
            fileStream.Close();

            RCON rcon2 = new RCON(IPAddress.Parse("127.0.0.1"), (ushort)25575, "test_password");

            FMCBridge.Running = true;
            Wrapper.Command("InputMode 3");
            Wrapper.WriteLine("Use \"wrapper InputMode 0\" to access Wrapper mode.");

            /*=======================================
            This loop monitors for user input in the console
            and sends it to the appropriate process
            =======================================*/
            string ConsoleInput = "";
            System.Threading.Tasks.Task.Run
                (async () =>
                    {
                        do {
                            if (Console.IsInputRedirected == false) {
                                //BridgeInput = await Console.In.ReadLineAsync();
                                ConsoleInput = await Console.In.ReadLineAsync();
                                //BridgeInput = Console.ReadLine();
                            }
                        } while (FMCBridge.Running == true);
                    }
                );
            List<ItemPair> factorioItems;
            List<ItemPair> minecraftItems;
            do {
                try {
                    //If the user wasn't a squit
                    if (string.IsNullOrWhiteSpace(ConsoleInput) == false) {
                        switch (Wrapper.InputTarget) {
                            //Default to wrapper
                            case Wrapper.Modes.Menu:
                                Wrapper.Command(ConsoleInput);
                                break;
                            case Wrapper.Modes.MinecraftServer:
                                if (MinecraftServer.Running) {
                                    MinecraftServer.ProcessInput(ConsoleInput);
                                }
                                break;
                            case Wrapper.Modes.FactorioServer:
                                if (FactorioServer.Running) {
                                    FactorioServer.ProcessInput(ConsoleInput);
                                }
                                break;
                            case Wrapper.Modes.FMCBridge:
                                FMCBridge.ProcessInput(ConsoleInput);
                                break;
                            default:
                                Wrapper.InputTarget = Wrapper.Modes.Menu;
                                throw new TrashMonkeyException("Invalid input mode! Defaulting to wrapper mode.");
                        }
                    }
                    factorioItems = parseFactorio(itemMappings, factorioRatios);
                    minecraftItems = parseVanillaMinecraft(itemMappings, minecraftRatios, rcon2).Result;
                    sendToFactorioExperimentalIO(minecraftItems);
                    sendToVanillaExperimentalIO(factorioItems);
                }
                catch (TrashMonkeyException e) { //Handled errors
                    Wrapper.ErrorWriteLine(e.Message);
                }
                catch (Exception e) {            //Something actually broke errors
                    Util.PrintErrorInfo(e);
                }
                ConsoleInput = "";
            } while (FMCBridge.Running == true);
            //Exiting this loop should return to the menu
        }

        public static void ProcessInput(string InputText)
        {
            //Send it to the wrapper if it's a wrapper command
            if (InputText.StartsWith("wrapper")) {
                Wrapper.Command(InputText.Remove(0, 8));
            }
            else if (InputText == "/quit") {
                FMCBridge.Running = false;
                FMCBridge.StopRoutine();
            }
            //Send it to the Factorio server
            else {
                //FactorioServer.Input.WriteLine(InputText);
            }
        }

        public static void StopRoutine() {
            MinecraftServer.StopRoutine();
            FactorioServer.StopRoutine();
            Wrapper.Mode = Wrapper.Modes.Menu;
            Wrapper.WriteLine("FMCBridge stopped successfully!");
        }

        private static string ToMCPath = Path.Combine(FactorioServer.ScriptOutputPath, "toMC.dat");
        private static Regex regex = new Regex(@"^.*?(?<=has the following block data: \[)|(?=\]-?[0-9]*, -?[0-9]*, -?[0-9]* has the following block data: \[).*?(?<=has the following block data: \[)|\]$", RegexOptions.Compiled);
        private static Regex regex2 = new Regex(@"{([^{}]+|(?<Level>{)|(?<-Level>}))+(?(Level)(?!))}", RegexOptions.Compiled);
        //private static Regex regex3 = new Regex(@",(?![^[]]*\))",RegexOptions.Compiled); <-- This regex wouldn't cooperate
        private static StringBuilder str = new StringBuilder();

        private static List<ItemPair> parseFactorio(DualDictionary<string, string> mappings, Dictionary<string, double> ratios) {
            List<ItemPair> items = new List<ItemPair>();
            using (FileStream fs = new FileStream(ToMCPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
            using (StreamReader sr = new StreamReader(fs, Encoding.Default)) {
                while (!sr.EndOfStream) {
                    string[] temp = sr.ReadLine().Split(':');
                    int count;
                    if (ratios.Count > 0) {
                        count = (int)Math.Round(double.Parse(temp[1]) * ratios[temp[0]], MidpointRounding.AwayFromZero);
                    }
                    else {
                        count = int.Parse(temp[1]);
                    }
                    int containsTest = pairContains(items, temp[0]);

                    if (containsTest != -1) {
                        if (items[containsTest].count < 64) {
                            int remainder = 64 - items[containsTest].count;
                            if (remainder > 0) {
                                items[containsTest].count += remainder;
                                count -= remainder;
                            }
                        }
                        if ((64 - count) < 0) {
                            items.Add(new ItemPair(temp[0], 64));
                            int remain = Math.Abs(64 - count);
                            while (remain > 0) {
                                if (remain > 64) {
                                    items.Add(new ItemPair(temp[0], 64));
                                    remain -= 64;
                                }
                                else {
                                    items.Add(new ItemPair(temp[0], remain));
                                    remain -= 64;
                                }
                            }
                        }
                        else {
                            items.Add(new ItemPair(temp[0], int.Parse(temp[1])));
                        }
                    }
                    else {
                        if ((64 - count) < 0) {
                            items.Add(new ItemPair(temp[0], 64));
                            int remain = Math.Abs(64 - count);
                            while (remain > 0) {
                                if (remain > 64) {
                                    items.Add(new ItemPair(temp[0], 64));
                                    remain -= 64;
                                }
                                else {
                                    items.Add(new ItemPair(temp[0], remain));
                                    remain -= 64;
                                }
                            }
                        }
                        else {
                            items.Add(new ItemPair(temp[0], int.Parse(temp[1])));
                        }
                        //items.Add(new ItemPair(temp[0], Int32.Parse(temp[1])));
                    }
                }
            }

            //Remap Items to the opposing item
            for (int i = 0; i < items.Count; i++) {
                items[i].name = mappings.factorio[items[i].name];
            }
            return items;
        }

        private static string ToFactorioString1 = @"/silent-command remote.call(""receiveItems"",""inputItems"",""";
        private static string ToFactorioString2 = @""",100)";

        private static void sendToFactorioExperimentalIO(List<ItemPair> items) {
            if (items.Count > 0) {
                for (int i = 0; i < items.Count; i++) {
                    if (items[i].count > 100) {
                        while (items[i].count > 100) {
                            str.Append(ToFactorioString1);
                            str.Append(items[i].name);
                            str.Append(ToFactorioString2);
                            FactorioServer.Input.WriteLine(str.ToString());
                            items[i].count -= 100;
                            str.Clear();
                        }
                    }
                    str.Append(@"/silent-command remote.call(""receiveItems"",""inputItems"",""");
                    str.Append(items[i].name);
                    str.Append(@""",");
                    str.Append(items[i].count);
                    str.Append(")");
                    FactorioServer.Input.WriteLine(str.ToString());
                    str.Clear();
                }
            }
        }

        private static async System.Threading.Tasks.Task<List<ItemPair>> parseVanillaMinecraft(DualDictionary<string, string> mappings, Dictionary<string, double> ratios, RCON rcon) {
            string RCON_Output = await rcon.SendCommandAsync("execute as @e[tag=SendChest] at @s store result score @s FMCClearItems run data get block ~ ~ ~ Items");
            //string toFactorioString = "";
            if (RCON_Output != "") {
                string[] SplitOutput = regex.Split(RCON_Output);
                string[][][] ParsedOutput = new string[SplitOutput.Length - 2][][];
                for (int i = 0; i < (SplitOutput.Length - 2); i++) {
                    MatchCollection MatchOutput2 = regex2.Matches(SplitOutput[i + 1]);
                    ParsedOutput[i] = new string[MatchOutput2.Count][];
                    for (int j = 0; j < MatchOutput2.Count; j++) {
                        string[] SplitOutput3 = MatchOutput2[j].ToString().Split(new[] { ',' }, 4);
                        ParsedOutput[i][j] = new string[SplitOutput3.Length];
                        for (int k = 0; k < (SplitOutput3.Length); k++) {
                            ParsedOutput[i][j][k] = SplitOutput3[k].Trim();
                        }
                    }
                }
                for (int i = 0; i < ParsedOutput.Length; i++) {
                    for (int j = 0; j < ParsedOutput[i].Length; j++) {
                        if (ParsedOutput[i][j].Length == 3) {
                            str.Append(ParsedOutput[i][j][1].Remove(0, 5).TrimEnd('"'));
                            str.Append("~");
                            str.Append(ParsedOutput[i][j][2].Remove(0, 7).TrimEnd('}').TrimEnd('b'));
                        }
                        else {
                            str.Append(ParsedOutput[i][j][1].Remove(0, 5).TrimEnd('"'));
                            str.Append(ParsedOutput[i][j][3].Remove(ParsedOutput[i][j][3].Length - 1, 1).Remove(0, 5));
                            str.Append("~");
                            str.Append(ParsedOutput[i][j][2].Remove(0, 7).TrimEnd('b'));
                        }
                        str.AppendLine();
                    }
                }
                //toFactorioString = str.ToString().TrimEnd(Environment.NewLine.ToCharArray());
                //toFactorioString = str.ToString();
            }
            List<ItemPair> items = new List<ItemPair>();
            //String[] temp7 = str.ToString().TrimEnd(Environment.NewLine.ToCharArray()).Split(Environment.NewLine.ToCharArray());
            foreach (string stringy3 in str.ToString().TrimEnd(Environment.NewLine.ToCharArray()).Split(Environment.NewLine.ToCharArray())) {
                if (string.IsNullOrWhiteSpace(stringy3) == false) {
                    string[] temp = stringy3.Split('~');
                    int count;
                    if (ratios.Count > 0) {
                        count = (int)Math.Round(double.Parse(temp[1]) * ratios[temp[0]], MidpointRounding.AwayFromZero);
                    }
                    else {
                        count = int.Parse(temp[1]);
                    }
                    items.Add(new ItemPair(temp[0], count));
                }
            }

            //Remap Items to the opposing item
            for (int i = 0; i < items.Count; i++) {
                try {
                    items[i].name = mappings.minecraft[items[i].name];
                }
                catch (Exception) {
                    //PrintErrorInfo(e);
                    continue;
                }
            }
            return items;
        }

        private static int ShulkerBoxSize = 27; //I wish this didn't have to be here, but I don't have any better ideas for how to avoid choking RCON.

        private static string[] sendToVanillaMinecraft(List<ItemPair> items) {//, StreamWriter MinecraftInput)
            string[][] InputItems = new string[items.Count][];
            string[] TempSplit;
            for (int i = 0; i < InputItems.Length; i++) {
                TempSplit = items[i].name.Split(Util.LeftCurlySplitter, 2);
                InputItems[i] = new string[3];
                InputItems[i][0] = TempSplit[0];
                try {
                    InputItems[i][1] = TempSplit[1];
                }
                catch (Exception) {
                    InputItems[i][1] = "";
                }
                InputItems[i][2] = items[i].count.ToString();
            }
            /*
                This section will break if more than 27 items are given at once since that maxes out a shulker box.
                I'll get around to fixing that soon.
                UPDATE: Added code to support multiple chests, but it still doesn't work for some reason.
                UPDATE2: Turns out it isn't exactly a problem with my code. It's actually trying to shove too much crap through RCON at once, thus giving it a heart attack.
                UPDATE3: I've attempted to redirect the IO of this program directly to the Minecraft server instead of using RCON. Idk how well that will work though.
            */
            int ShulkerBoxCount = ((InputItems.Length - 1) / ShulkerBoxSize);
            int ShulkerBoxOverflow = (InputItems.Length % ShulkerBoxSize);
            string[][] ItemStrings = new string[(ShulkerBoxCount + 1)][];
            if (ShulkerBoxOverflow != 0) {
                for (int i = 0; i < (ItemStrings.Length - 1); i++) {
                    ItemStrings[i] = new string[ShulkerBoxSize];
                }
                ItemStrings[(ItemStrings.Length - 1)] = new string[ShulkerBoxOverflow];
            }
            else {
                for (int i = 0; i < ItemStrings.Length; i++) {
                    ItemStrings[i] = new string[ShulkerBoxSize];
                }
            }
            int ItemIndex;
            string[] ShulkerBoxStrings = new string[ItemStrings.Length];
            for (int i = 0; i < ItemStrings.Length; i++) {
                ShulkerBoxStrings[i] = "[";
                for (int j = 0; j < ItemStrings[i].Length; j++) {
                    ItemIndex = ((ShulkerBoxSize * i) + j);
                    if (InputItems[ItemIndex][1] != "") {
                        ItemStrings[i][j] = "{Slot:" + j + "b,id:\"" + InputItems[ItemIndex][0] + "\",Count:" + InputItems[ItemIndex][2] + "b,tag:{" + InputItems[ItemIndex][1] + "}";
                    }
                    else {
                        ItemStrings[i][j] = "{Slot:" + j + "b,id:\"" + InputItems[ItemIndex][0] + "\",Count:" + InputItems[ItemIndex][2] + "b}";
                    }
                    ShulkerBoxStrings[i] += ItemStrings[i][j];
                    if (j != (ItemStrings[i].Length - 1)) {
                        ShulkerBoxStrings[i] += ",";
                    }
                }
                ShulkerBoxStrings[i] += "]";
            }
            return ShulkerBoxStrings;
        }
        private static void sendToVanillaExperimentalIO(List<ItemPair> items)
        {
            if (items.Count > 0) {
                string[] StringsToSend = sendToVanillaMinecraft(items);
                for (int i = 0; i < StringsToSend.Length; i++) {
                    MinecraftServer.Input.WriteLine("execute as @e[tag=ReceiveChest,tag=!ReceiveFull,limit=1,sort=arbitrary,scores={FMCCollectItems=0}] store result score @s FMCCollectItems at @s run setblock ~ 0 ~ minecraft:shulker_box{Items:" + StringsToSend[i] + "}");
                }
            }
        }

        private static int pairContains(List<ItemPair> list, string itemName) {
            for (int i = 0; i < list.Count(); i++) {
                if (list[i].name.Equals(itemName)) {
                    return i;
                }
            }
            return -1;
        }
    }
}
