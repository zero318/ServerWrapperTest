using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.IO;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Net;
using CoreRCON;
using CoreRCON.Parsers.Standard;
using Newtonsoft.Json;
using Ini;
using SuccExceptions;

namespace MCServerWrapperTest1
{
    class Program
    {
        static string WrapperVersion = "0.2";
        static bool RunWrapper = true;
        static byte WrapperMode = 0;
        static byte ConsoleMode = 0;

        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            ConsoleMode = 0;
            WrapperWriteLine("Starting Up Wrapper!");

            while (RunWrapper)
            {
                switch (WrapperMode)
                {
                    case 0: //Wrapper Menu
                        Console.Title = "Server Wrapper " + WrapperVersion;
                        WrapperWriteLine("Wrapper Menu:");
                        WrapperWriteLine("0. Start Minecraft Server");
                        WrapperWriteLine("q. Exit Wrapper");
                        bool ValidSelection = false;
                        while (ValidSelection == false)
                        {
                            ValidSelection = true;
                            switch (Console.ReadKey().KeyChar)
                            {
                                case '0':
                                    WrapperMode = 1;
                                    break;
                                case 'q':
                                    RunWrapper = false;
                                    break;
                                default:
                                    ValidSelection = false;
                                    WrapperErrorWriteLine("Unrecognized menu selection!");
                                    break;
                            }
                        }
                        break;
                    case 1: //Run MC Server
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        ConsoleMode = 0;
                        WrapperWriteLine("Starting Minecraft server...");
                        string SettingsFileName = "Settings.ini";
                        string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);
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
                        IniFile s = new IniFile(SettingsFileName);
                        Console.Title = "Wrapper." + WrapperVersion + " MinecraftServer." + s.Read("Version", "Minecraft");
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

                        Process MCServer = new Process();
                        MCServer.StartInfo.FileName = s.Read("Executable", "Java");
                        MCServer.StartInfo.Arguments = ArgumentsString;
                        MCServer.StartInfo.CreateNoWindow = false;
                        MCServer.StartInfo.WorkingDirectory = ServerPath;
                        MCServer.StartInfo.ErrorDialog = false;
                        MCServer.StartInfo.UseShellExecute = false;
                        MCServer.StartInfo.RedirectStandardError = true;
                        MCServer.StartInfo.RedirectStandardOutput = true;
                        MCServer.StartInfo.RedirectStandardInput = true;
                        string MCServerLog = "";
                        MCServer.OutputDataReceived += new DataReceivedEventHandler
                        (
                            (sender, args2) =>
                            {
                                ConsoleColor OldColor = Console.ForegroundColor;
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("[Minecraft] " + args2.Data);
                                MCServerLog = args2.Data + "";
                                Console.ForegroundColor = OldColor;
                            }
                        );
                        MCServer.ErrorDataReceived += new DataReceivedEventHandler
                        (
                            (sender, args2) =>
                            {
                                ConsoleColor OldColor = Console.ForegroundColor;
                                Console.ForegroundColor = ConsoleColor.DarkGreen;
                                Console.WriteLine("[Minecraft Error] " + args2.Data);
                                MCServerLog = args2.Data;
                                Console.ForegroundColor = OldColor;
                            }
                        );
                        MCServer.Start();
                        StreamWriter MinecraftInputWriter = MCServer.StandardInput;
                        MCServer.BeginOutputReadLine();
                        MCServer.BeginErrorReadLine();
                        while (!MCServerLog.Contains("[Server thread/INFO]: Done (")) ;
                        WrapperWriteLine("Minecraft server loaded!");

                        string ConsoleInput = "";
                        System.Threading.Tasks.Task.Run
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
                        );

                        WrapperCommand("InputMode 1");
                        WrapperWriteLine("Use \"wrapper InputMode 0\" to access Wrapper mode.");

                        bool RunServer = true;
                        while (RunServer)
                        {
                            try
                            {
                                if (string.IsNullOrEmpty(ConsoleInput) == false)
                                {
                                    switch (ConsoleMode)
                                    {
                                        case 0:
                                            WrapperWriteLine(WrapperCommand(ConsoleInput));
                                            break;
                                        case 1:
                                            if (ConsoleInput.StartsWith("wrapper"))
                                            {
                                                WrapperWriteLine(WrapperCommand(ConsoleInput.Remove(0, 7)));
                                            }
                                            else
                                            {
                                                if (ConsoleInput != "stop") //If it's a stop command, switch to the handler routine
                                                {
                                                    MinecraftInputWriter.WriteLine(ConsoleInput);
                                                }
                                                else
                                                {
                                                    throw new SaveStopException();
                                                }
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
                            catch (SaveStopException)       //Quick workaround
                            {
                                RunServer = false;
                            }
                            catch (MinecraftInput e)
                            {
                                MinecraftInputWriter.WriteLine(e.Message);
                            }
                            catch (Exception e)             //Something actually broke errors
                            {
                                PrintErrorInfo(e);
                                ConsoleInput = "";
                            }
                            if (MCServerLog.Contains("[Server thread/INFO]: * zero318 smite "))
                            {
                                string[] smite_args = MCServerLog.Split(new string[] { "smite " }, StringSplitOptions.RemoveEmptyEntries);

                                MinecraftInputWriter.WriteLine("execute at " + smite_args[1] + " run summon minecraft:lightning_bolt ~ ~ ~");

                                MCServerLog = "";
                            }
                        }
                        //Save and Stop Routine
                        MinecraftInputWriter.WriteLine("save-all");
                        while (!MCServerLog.Contains("[Server thread/INFO]: Saved the game")) ;
                        MinecraftInputWriter.WriteLine("stop");
                        while (MCServer.HasExited == false) ;
                        WrapperMode = 0;
                        break;
                    default:
                        break;
                }
            }
            Environment.Exit(0);
        }

        public static string WrapperCommand(string Command)
        {
            string[] args = Command.Trim().Split(' ');
            switch (args[0].ToLower())
            {
                case "stop":
                case "stopwrapper":
                    throw new SaveStopException();
                case "inputmode":
                    if (args.Length > 1)
                    {
                        //Mode is specified, so switch to it
                        switch (args[1].ToLower())
                        {
                            case "0":
                            case "wrapper":
                                ConsoleMode = 0;
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                return "Switching to wrapper mode.";
                            case "1":
                            case "minecraft":
                                ConsoleMode = 1;
                                Console.ForegroundColor = ConsoleColor.Green;
                                return "Switching to minecraft mode.";
                            default:
                                throw new TrashMonkeyException("Invalid input mode!");
                        }
                    }
                    throw new TrashMonkeyException("Not enough arguments!");
                case "switchinputmode":
                    //Mode is not specified, so toggle the current mode
                    switch (ConsoleMode)
                    {
                        case 0:
                            ConsoleMode = 1;
                            Console.ForegroundColor = ConsoleColor.Green;
                            return "Switching to minecraft mode.";
                        case 1:
                            ConsoleMode = 0;
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            return "Switching to wrapper mode.";
                        default:
                            ConsoleMode = 0;
                            throw new TrashMonkeyException("Invalid console mode detected! Switching to wrapper mode.");
                    }
                default:
                    throw new TrashMonkeyException("Unrecognized wrapper command!");
            }
            throw new TrashMonkeyException("WTF did you do?");
        }

        public static void PrintErrorInfo(Exception e)
        {
            WrapperErrorWriteLine("Something went wrong. Moving past error.");
            WrapperErrorWriteLine("Error info:");
            WrapperErrorWriteLine(e.Message);
            WrapperErrorWriteLine(e.InnerException.ToString());
        }

        public static void WrapperWriteLine(string Line)
        {
            ConsoleColor OldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("[Wrapper] " + Line);
            Console.ForegroundColor = OldColor;
        }

        public static void WrapperErrorWriteLine(string Line)
        {
            ConsoleColor OldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("[Wrapper Error] " + Line);
            Console.ForegroundColor = OldColor;
        }
    }
}
