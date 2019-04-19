
/*=======================================
Minecraft Server Wrapper

This class runs a Minecraft server jar inside of a C# program.

This allows the C# program to read the server log as strings and
send input strings as if it were typing in the server console.

Here are a few quick notes for anyone who isn't familiar with
my style of code and/or C#:
1. Execution starts at Main().

=======================================*/


//I've commented out the includes that aren't being used at the moment.
using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Text.RegularExpressions;
//using System.Threading;
//using System.Xml;
using System.IO;
//using System.ComponentModel;
using System.Diagnostics;
//using System.Runtime.InteropServices;
//using System.Net;
//using CoreRCON;
//using CoreRCON.Parsers.Standard;
//using Newtonsoft.Json;
using Ini;
using SuccExceptions;

/*=======================================
Generic Block Comment Template
=======================================*/

namespace ServerWrapperTest
{
    class Wrapper
    {
        //These variables are accessible to everything in the class
        static readonly string Version = "0.3";
        static bool Run;
        static WrapperMode Mode;
        static WrapperMode InputTarget;
        enum WrapperMode : byte {Menu, MinecraftServer};

        //Execution starts here
        static void Main(string[] StartupArgs)
        {
            Wrapper.Run = true;
            Wrapper.InputTarget = WrapperMode.Menu;
            MinecraftServer.Input = null;
            WrapperWriteLine("Starting Up Wrapper!");

            //This line is just a convoluted way of setting the initial wrapper mode.
            //If I call the program from cmd with a mode argument, it'll set itself to that mode.
            //Otherwise it'll just default to the menu.
            Wrapper.Mode = Enum.IsDefined(typeof(WrapperMode), ((StartupArgs.Length != 0) ? StartupArgs[0] : "-1")) ? (WrapperMode)Enum.Parse(typeof(WrapperMode), StartupArgs[0]) : WrapperMode.Menu;

            while (Wrapper.Run == true) //Set this to false if too much dumb crap happens
            {
                switch (Wrapper.Mode)
                {
                    case WrapperMode.Menu: //Wrapper Menu
                        WrapperMenu();
                        break;
                    case WrapperMode.MinecraftServer: //Run MC Server
                        RunMinecraftServer();
                        break;
                    default:
                        break;
                }
            }
            //MinecraftServer.Input.Dispose();
            Environment.Exit(0);
        }
        private static void WrapperMenu()
        {
            Console.Title = "Server Wrapper " + Wrapper.Version;
            WrapperWriteLine("Wrapper Menu:\n0. Start Minecraft Server\nq. Exit Wrapper");
            //WrapperWriteLine("Wrapper Menu:");
            //WrapperWriteLine("0. Start Minecraft Server");
            //WrapperWriteLine("q. Exit Wrapper");
            bool ValidSelection = false;
            do
            {
                switch (Console.ReadKey().KeyChar)
                {
                    case '0':
                        Wrapper.Mode = WrapperMode.MinecraftServer;
                        ValidSelection = true;
                        break;
                    case 'q':
                        Wrapper.Run = false;
                        ValidSelection = true;
                        break;
                    default:
                        WrapperErrorWriteLine("Unrecognized menu selection!");
                        break;
                }
            } while (ValidSelection == false);
        }


        /*=======================================
        This subroutine starts up the server
        and then monitors the output.
        =======================================*/
        private static void RunMinecraftServer()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Wrapper.InputTarget = WrapperMode.Menu;

            WrapperWriteLine("Starting Minecraft server...");
            string SettingsFileName = "Settings.ini";
            string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);

            /*=======================================
            Create a settings file with some default values if it doesn't exist.
            These default values probably won't actually work, but it'll at least create the file.
            =======================================*/
            if (!File.Exists(SettingsFilePath))
            {
                IniFile DefaultSettings = new IniFile(SettingsFileName);
                DefaultSettings.Write("Version", "1.14-pre5", "Minecraft");
                DefaultSettings.Write("Arguments", "nogui", "Minecraft");
                DefaultSettings.Write("Enable", "false", "Fabric");
                DefaultSettings.Write("Version", "0.4.0+build.121", "Fabric");
                DefaultSettings.Write("Folder", @"E:\My_Minecraft_Expansion_2\Local Server", "Windows");
                DefaultSettings.Write("Executable", "java", "Java");
                DefaultSettings.Write("Type", "-d64 -server", "Java");
                DefaultSettings.Write("MemMax", "2G", "Java");
                DefaultSettings.Write("MemMin", "1G", "Java");
                DefaultSettings.Write("Arguments", "-XX:+UseConcMarkSweepGC -XX:+DisableExplicitGC -XX:+UseAdaptiveGCBoundary -XX:MaxGCPauseMillis=500 -XX:-UseGCOverheadLimit -XX:SurvivorRatio=12 -XX:NewRatio=4 -Xnoclassgc -XX:UseSSE=3", "Java");
            }

            /*=======================================
            Read the settings file and build a huge string with it
            =======================================*/
            IniFile s = new IniFile(SettingsFileName);
            Console.Title = "Wrapper." + Wrapper.Version + " MinecraftServer." + s.Read("Version", "Minecraft");
            string ServerPath = s.Read("Folder", "Windows") + '\\';
            string MinecraftJar = "minecraft_server." + s.Read("Version", "Minecraft") + ".jar";
            string ArgumentsString = "-Xmx" + s.Read("MemMax", "Java") + " -Xms" + s.Read("MemMin", "Java") +
                                        " " + s.Read("Type", "Java") + " " + s.Read("Arguments", "Java") +
                                        " -jar \"" + ServerPath;
            if (Convert.ToBoolean(s.Read("Enable", "Fabric")))
            {
                Console.Title = Console.Title + " FabricLoader." + s.Read("Version", "Fabric");
                string FabricJar = "fabric-loader-" + s.Read("Version", "Fabric") + ".jar";
                ArgumentsString += FabricJar + "\" \"" + ServerPath;
                File.WriteAllText(ServerPath + "fabric-server-launcher.properties", "serverJar=" + MinecraftJar);
            }
            ArgumentsString += MinecraftJar + "\" " + s.Read("Arguments", "Minecraft");

            /*=======================================
            Configure the server process before starting it
            =======================================*/
            MinecraftServer.Process = new Process();
            MinecraftServer.Process.StartInfo.FileName = s.Read("Executable", "Java");
            MinecraftServer.Process.StartInfo.Arguments = ArgumentsString;
            MinecraftServer.Process.StartInfo.CreateNoWindow = false;
            MinecraftServer.Process.StartInfo.WorkingDirectory = ServerPath;
            MinecraftServer.Process.StartInfo.ErrorDialog = false;
            MinecraftServer.Process.StartInfo.UseShellExecute = false;
            MinecraftServer.Process.StartInfo.RedirectStandardError = true;
            MinecraftServer.Process.StartInfo.RedirectStandardOutput = true;
            MinecraftServer.Process.StartInfo.RedirectStandardInput = true;
            MinecraftServer.Log = "";

            /*=======================================
            These are what read/print/process the server log.
            They'll be run whenever the server process outputs text,
            even while the code continues running below.
            =======================================*/
            MinecraftServer.Process.OutputDataReceived += new DataReceivedEventHandler
            (
                (sender, OutputText) =>
                {
                    ConsoleColor OldColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[Minecraft] " + OutputText.Data);
                    MinecraftServer.Log = OutputText.Data;
                    ProcessMinecraftLog(OutputText.Data);
                    Console.ForegroundColor = OldColor;
                }
            );
            MinecraftServer.Process.ErrorDataReceived += new DataReceivedEventHandler
            (
                (sender, ErrorText) =>
                {
                    ConsoleColor OldColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.WriteLine("[Minecraft Error] " + ErrorText.Data);
                    //MinecraftServer.Log = ErrorText.Data;
                    Console.ForegroundColor = OldColor;
                }
            );

            /*=======================================
            Finally start the dang server process
            =======================================*/
            MinecraftServer.Process.Start();

            //Redirect the input so that the code can send text
            MinecraftServer.Input = MinecraftServer.Process.StandardInput;

            //Start checking for output
            MinecraftServer.Process.BeginOutputReadLine();
            MinecraftServer.Process.BeginErrorReadLine();

            //Don't try to do anything else until the server finishes loading
            while (!MinecraftServer.Log.Contains("[Server thread/INFO]: Done (")) ;
            WrapperWriteLine("Minecraft server loaded!");

            string ConsoleInput = "";

            //Ignore this bit

            /*System.Threading.Tasks.Task.Run
            (async () =>
            {
                while (true)
                {
                    if (Console.IsInputRedirected == false)
                    {
                        ConsoleInput = await Console.In.ReadLineAsync();
                    }
                }
            }
            );*/

            WrapperCommand("InputMode 1");
            WrapperWriteLine("Use \"wrapper InputMode 0\" to access Wrapper mode.");

            /*=======================================
            This loop monitors for user input in the console
            and sends it to the appropriate process
            =======================================*/
            MinecraftServer.Run = true;
            do
            {
                try
                {
                    //Get user input
                    ConsoleInput = Console.In.ReadLine();

                    //If the user wasn't a squit
                    if (string.IsNullOrWhiteSpace(ConsoleInput) == false)
                    {
                        switch (Wrapper.InputTarget)
                        {
                            //Default to wrapper
                            case WrapperMode.Menu:
                                WrapperWriteLine(WrapperCommand(ConsoleInput));
                                break;
                            //Default to Minecraft server
                            case WrapperMode.MinecraftServer:
                                //Send it to the wrapper if it's a wrapper command
                                if (ConsoleInput.StartsWith("wrapper"))
                                {
                                    WrapperWriteLine(WrapperCommand(ConsoleInput.Remove(0, 7)));
                                }
                                //If it's a stop command, switch to the wrapper stop routine
                                else if (ConsoleInput == "stop")
                                {
                                    WrapperWriteLine(StopRoutine());
                                }
                                //Send it to the Minecraft server
                                else
                                {
                                    MinecraftServer.Input.WriteLine(ConsoleInput);
                                }
                                break;
                        }
                        ConsoleInput = "";
                    }
                }
                catch (TrashMonkeyException e)  //Handled errors
                {
                    WrapperErrorWriteLine(e.Message);
                    ConsoleInput = "";
                }
                catch (Exception e)             //Something actually broke errors
                {
                    PrintErrorInfo(e);
                    ConsoleInput = "";
                }
            } while (MinecraftServer.Run == true);
            //Exiting this loop should return to the menu
        }

        /*=======================================
        This is where the program detects and
        processes custom Minecraft commands
        =======================================*/
        private static void ProcessMinecraftLog(string LogText)
        {
            if (string.IsNullOrWhiteSpace(LogText) == false)
            {
                if (LogText.Contains("[Server thread/INFO]: * zero318 smite "))
                {
                    string[] smite_args = LogText.Split(new string[] { "smite " }, StringSplitOptions.RemoveEmptyEntries);

                    MinecraftServer.Input.WriteLine("execute at " + smite_args[1] + " run summon minecraft:lightning_bolt ~ ~ ~");
                }
            }
        }

        /*=======================================
        This is where the program detects and
        processes wrapper commands
        =======================================*/
        private static string WrapperCommand(string Command)
        {
            string[] CommandArguments = Command.Trim().Split(' ');
            switch (CommandArguments[0].ToLower())
            {
                case "stop":
                case "stopwrapper":
                    return StopRoutine();
                case "inputmode":
                    if (CommandArguments.Length > 1)
                    {
                        //Mode is specified, so switch to it
                        switch (CommandArguments[1].ToLower())
                        {
                            case "0":
                            case "wrapper":
                                return SwitchToWrapperMode();
                            case "1":
                            case "minecraft":
                                return SwitchToMinecraftMode();
                            default:
                                throw new TrashMonkeyException("Invalid input mode!");
                        }
                    }
                    throw new TrashMonkeyException("Not enough arguments!");
                case "switchinputmode":
                    //Mode is not specified, so just toggle the current mode
                    switch (Wrapper.InputTarget)
                    {
                        case WrapperMode.Menu:
                            return SwitchToMinecraftMode();
                        case WrapperMode.MinecraftServer:
                            return SwitchToWrapperMode();
                        default:
                            Wrapper.InputTarget = WrapperMode.Menu;
                            throw new TrashMonkeyException("Invalid console mode detected! Switching to wrapper mode.");
                    }
                default:
                    throw new TrashMonkeyException("Unrecognized wrapper command!");
            }
            throw new TrashMonkeyException("WTF did you do?");
        }

        /*=======================================
        Saves the world and stops the server
        =======================================*/
        private static string StopRoutine()
        {
            MinecraftServer.Run = false;
            MinecraftServer.Input.WriteLine("save-all");
            //Wait for the save to finish
            while (!MinecraftServer.Log.Contains("[Server thread/INFO]: Saved the game")) ;
            MinecraftServer.Input.WriteLine("stop");
            //Make sure the server process has stopped
            while (MinecraftServer.Process.HasExited == false) ;
            Wrapper.Mode = WrapperMode.Menu;
            MinecraftServer.Input = null;
            return "Server stopped successfully!";
        }

        /*=======================================
        This is the "unhandled errors" printer
        =======================================*/
        private static void PrintErrorInfo(Exception e)
        {
            WrapperErrorWriteLine("Something went wrong. Moving past error.");
            WrapperErrorWriteLine("Error info:");
            WrapperErrorWriteLine(e.Message);
            WrapperErrorWriteLine(e.InnerException.ToString());
        }

        /*=======================================
        These just print text in the appropriate colors
        =======================================*/
        private static void WrapperWriteLine(string Line)
        {
            ConsoleColor OldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("[Wrapper] " + Line);
            Console.ForegroundColor = OldColor;
        }

        private static void WrapperErrorWriteLine(string Line)
        {
            ConsoleColor OldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("[Wrapper Error] " + Line);
            Console.ForegroundColor = OldColor;
        }

        /*=======================================
        These save a few lines of code up above so
        I don't have to copy it several times
        =======================================*/
        private static string SwitchToWrapperMode()
        {
            Wrapper.InputTarget = WrapperMode.Menu;
            Console.ForegroundColor = ConsoleColor.Cyan;
            return "Switching to wrapper mode.";
        }
        private static string SwitchToMinecraftMode()
        {
            Wrapper.InputTarget = WrapperMode.MinecraftServer;
            Console.ForegroundColor = ConsoleColor.Green;
            return "Switching to Minecraft mode.";
        }
    }

    /*=======================================
    I'll shove the actual server code into this class later.
    For now it's just here for variable access.
    =======================================*/
    class MinecraftServer
    {
        public static Process Process;

        public static bool Run;
        public static string Log;

        //I moved this up here since I was having errors accessing it in subroutines.
        //I'm not sure what the proper way of doing it is, but this'll work.
        public static StreamWriter Input;
    }
}
