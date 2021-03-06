﻿
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
using System.IO;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Text.RegularExpressions;
//using System.Threading;
//using System.Xml;
//using System.ComponentModel;
//using System.Runtime.InteropServices;
//using System.Net;
//using CoreRCON;
//using CoreRCON.Parsers.Standard;
//using Newtonsoft.Json;
using SuccExceptions;

/*=======================================
Generic Block Comment Template
=======================================*/

namespace ServerWrapperTest
{
    class Wrapper
    {
        public static readonly Util.LogFormat OutputFormat = new Util.LogFormat("[Wrapper] ", ConsoleColor.Cyan);
        public static readonly Util.LogFormat ErrorFormat = new Util.LogFormat("[Wrapper Error] ", ConsoleColor.DarkCyan);

        public static readonly string Version = "0.8";
        private static bool Run;
        public static Modes Mode;
        public static Modes InputTarget;

        public enum Modes : byte { Menu, MinecraftServer, FactorioServer, FMCBridge, UnturnedServer, SRB2KServer };

        //Execution starts here
        static void Main(string[] StartupArgs)
        {
            //For testing, uncomment this line.
            //_DebugTest.Test();

            //This makes the program not do something stupid if you press Ctrl+C
            //Console.TreatControlCAsInput = true;
            Wrapper.Run = true;
            Wrapper.InputTarget = Wrapper.Modes.Menu;
            Wrapper.WriteLine("Starting Up Wrapper!");

            //This line is just a convoluted way of setting the initial wrapper mode.
            //If I call the program from cmd with a mode argument, it'll set itself to that mode.
            //Otherwise it'll just default to the menu.
            Wrapper.Mode = Enum.IsDefined(typeof(Wrapper.Modes), ((StartupArgs.Length != 0) ? StartupArgs[0] : "-1")) ? (Wrapper.Modes)Enum.Parse(typeof(Wrapper.Modes), StartupArgs[0]) : Wrapper.Modes.Menu;

            while (Wrapper.Run == true) //Set this to false if too much dumb crap happens
            {
                switch (Wrapper.Mode)
                {
                    case Wrapper.Modes.Menu: //Wrapper Menu
                        Wrapper.Menu();
                        break;
                    case Wrapper.Modes.MinecraftServer: //Run MC Server
                        MinecraftServer.Run();
                        break;
                    case Wrapper.Modes.FactorioServer:
                        FactorioServer.Run();
                        break;
                    case Wrapper.Modes.FMCBridge:
                        FMCBridge.Run();
                        break;
                    case Wrapper.Modes.UnturnedServer:
                        UnturnedServer.Run();
                        break;
                    //case Wrapper.Modes.SRB2KServer:
                    //    SRB2KServer.Run();
                    //    break;
                    default:
                        Wrapper.ErrorWriteLine("Invalid mode!");
                        break;
                }
            }
            Environment.Exit(0);
        }

        public static void Menu()
        {
            Console.Title = "Server Wrapper " + Wrapper.Version;
            Wrapper.WriteLine("Wrapper Menu:\n" +
                              "1. Start Minecraft Server\n" +
                              "a. Start Minecraft Server (Fabric)\n" +
                              "2. Start Factorio Server\n" +
                              "3. Start Minecraft Factorio Bridge\n" +
                              "b. Start Minecraft Factorio Bridge (Fabric)\n" +
                              "4. Start Unturned Server\n" +
                              "5. Start SRB2K Server\n" +
                              "q. Exit Wrapper");
            bool ValidSelection = false;
            do
            {
                switch (Console.ReadKey().KeyChar)
                {
                    case '1':
                        Wrapper.Mode = Wrapper.Modes.MinecraftServer;
                        MinecraftServer.IsFabric = false;
                        ValidSelection = true;
                        break;
                    case 'a':
                        Wrapper.Mode = Wrapper.Modes.MinecraftServer;
                        MinecraftServer.IsFabric = true;
                        ValidSelection = true;
                        break;
                    case '2':
                        Wrapper.Mode = Wrapper.Modes.FactorioServer;
                        ValidSelection = true;
                        break;
                    case '3':
                        Wrapper.Mode = Wrapper.Modes.FMCBridge;
                        MinecraftServer.IsFabric = false;
                        ValidSelection = true;
                        break;
                    case 'b':
                        Wrapper.Mode = Wrapper.Modes.FMCBridge;
                        MinecraftServer.IsFabric = true;
                        ValidSelection = true;
                        break;
                    case '4':
                        Wrapper.Mode = Wrapper.Modes.UnturnedServer;
                        ValidSelection = true;
                        break;
                    //case '5':
                    //    Wrapper.Mode = Wrapper.Modes.SRB2KServer;
                    //    ValidSelection = true;
                    //    break;
                    case 'q':
                        Wrapper.Run = false;
                        ValidSelection = true;
                        break;
                    default:
                        Wrapper.ErrorWriteLine("Unrecognized menu selection!");
                        break;
                }
            } while (ValidSelection == false);
        }

        /*=======================================
        This is where the program detects and
        processes wrapper commands

        It's also probably the crappiest section of the code.
        I'm sure there's a better way of handling this than just string split.
        =======================================*/
        public static void Command(string Command)
        {
            try {
                string[] CommandArguments = Command.Trim().Split(' ');
                switch (CommandArguments[0].ToLower()) {
                    case "wrapper":
                        Wrapper.Command(Command.Remove(0, 8));
                        break;
                    case "stop":
                    case "stopwrapper":
                        //if (MinecraftServer.Running)
                        //{
                        //    MinecraftServer.StopRoutine();
                        //}

                        break;
                    case "inputmode":
                        if (CommandArguments.Length > 1)
                        {
                            //Mode is specified, so switch to it
                            switch (CommandArguments[1].ToLower())
                            {
                                case "0":
                                case "wrapper":
                                    Util.SetInputTarget(Wrapper.Modes.Menu);
                                    break;
                                case "1":
                                case "minecraft":
                                    Util.SetInputTarget(Wrapper.Modes.MinecraftServer);
                                    break;
                                case "2":
                                case "factorio":
                                    Util.SetInputTarget(Wrapper.Modes.FactorioServer);
                                    break;
                                case "3":
                                case "bridge":
                                case "fmcbridge":
                                    Util.SetInputTarget(Wrapper.Modes.FMCBridge);
                                    break;
                                case "4":
                                case "unturned":
                                    Util.SetInputTarget(Wrapper.Modes.UnturnedServer);
                                    break;
                                case "5":
                                case "kart":
                                case "srb2k":
                                    Util.SetInputTarget(Wrapper.Modes.SRB2KServer);
                                    break;
                                case "?":
                                    Wrapper.WriteLine("Valid InputModes:\n" +
                                                      "0 Wrapper\n" +
                                                      "1 Minecraft Server\n" +
                                                      "2 Factorio Server\n" +
                                                      "3 FMCBridge\n" +
                                                      "4 Unturned Server\n" +
                                                      "5 SRBK2 Server");
                                    break;
                                default:
                                    throw new TrashMonkeyException("Invalid input mode!");
                            }
                        }
                        else
                        {
                            throw new TrashMonkeyException("Not enough arguments!");
                        }
                        break;
                    case "switchinputmode":
                        //Mode is not specified, so just toggle the current mode
                        switch (Wrapper.InputTarget)
                        {
                            case Wrapper.Modes.Menu:
                                Util.SetInputTarget(Wrapper.Modes.MinecraftServer);
                                break;
                            case Wrapper.Modes.MinecraftServer:
                                Util.SetInputTarget(Wrapper.Modes.FactorioServer);
                                break;
                            case Wrapper.Modes.FactorioServer:
                                Util.SetInputTarget(Wrapper.Modes.FMCBridge);
                                break;
                            case Wrapper.Modes.FMCBridge:
                                Util.SetInputTarget(Wrapper.Modes.UnturnedServer);
                                break;
                            case Wrapper.Modes.UnturnedServer:
                                Util.SetInputTarget(Wrapper.Modes.Menu);
                                break;
                            //case Wrapper.Modes.SRB2KServer:
                            //    Util.SetInputTarget(Wrapper.Modes.Menu);
                            //    break;
                            default:
                                Wrapper.InputTarget = Wrapper.Modes.Menu;
                                throw new TrashMonkeyException("Invalid console mode detected! Switching to wrapper mode.");
                        }
                        break;
                    case "?":
                        Wrapper.WriteLine("Valid Wrapper Commands:\n" +
                                          "stopwrapper\n-Stops whatever servers are running\n" +
                                          "inputmode\n-Changes the default input mode\n" +
                                          "switchinputmode\n-Cycles the default input mode\n" +
                                          "?\n-Displays this list");
                        break;
                    default:
                        throw new TrashMonkeyException("Unrecognized wrapper command!");
                }
            }
            catch (TrashMonkeyException e) {
                Wrapper.ErrorWriteLine(e.Message);
            }
            catch (Exception e) {
                Util.PrintErrorInfo(e);
            }
            
        }

        /*=======================================
        These just print text in the appropriate colors
        =======================================*/
        public static void WriteLine(string Line)
        {
            Util.WriteToLog(Line, Wrapper.OutputFormat);
        }

        public static void ErrorWriteLine(string Line)
        {
            Util.WriteToLog(Line, Wrapper.ErrorFormat);
        }
    }
}
