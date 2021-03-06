﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Ini;
using SuccExceptions;

namespace ServerWrapperTest {
    class FactorioServer {
        [DllImport("kernel32.dll")]
        static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        static extern bool AllocConsole();

        public static readonly Util.LogFormat OutputFormat = new Util.LogFormat("[Factorio] ", ConsoleColor.Red);
        public static readonly Util.LogFormat ErrorFormat = new Util.LogFormat("[Factorio Error] ", ConsoleColor.DarkRed);

        private static Process Process;

        private static bool IsBridge = false;

        public static bool Running = false;
        private static bool Loaded;
        private static bool Stopping;
        private static bool NoConsole = false;
        private static string PIDFile;

        public static string RootPath;
        public static string ScriptOutputPath;

        public static StreamWriter Input;
        /*=======================================
        This subroutine starts up the server
        and then monitors the output.
        =======================================*/
        public static void Run(bool BridgeMode = false) {
            FactorioServer.IsBridge = BridgeMode;
            FactorioServer.Loaded = false;
            FactorioServer.Stopping = false;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Wrapper.InputTarget = Wrapper.Modes.Menu;

            Wrapper.WriteLine("Preparing Factorio server...");
            string SettingsFileName = "Settings.ini";
            string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);

            /*=======================================
            Create a settings file with some default values if it doesn't exist.
            =======================================*/
            if (!File.Exists(SettingsFilePath)) {
                IniFile ds = new IniFile(SettingsFileName);
                ds.Write("Version", "0.18.10", "Factorio");
                ds.Write("ExePath", @"E:\Factorio\Game\steamapps\common\Factorio\bin\x64\factorio.exe", "Factorio");
                ds.Write("ServerArgs", @"--start-server E:\Factorio\Data\saves\Minecraft_Bridge_Tests\Minecraft_Bridge_Test_1 --bind 127.0.0.1:30000 --no-log-rotation", "Factorio");
            }

            /*=======================================
            Read the settings file to find the server path
            =======================================*/
            Wrapper.WriteLine("Reading wrapper settings file...");
            IniFile s = new IniFile(SettingsFileName);
            Console.Title = "Wrapper." + Wrapper.Version + " FactorioServer." + s.Read("Version", "Factorio");
            RootPath = s.Read("ServerFolder", "Windows") + '\\';

            ScriptOutputPath = s.Read("ScriptOutputPath", "Factorio") + '\\';

            /*=======================================
            Setup some files related to logs and crap
            =======================================*/
            Wrapper.WriteLine("Checking for previous unstopped servers...");
            PIDFile = RootPath + s.Read("PIDFile", "Factorio");
            if (File.Exists(PIDFile))
            {
                Int32 PreviousPID = Int32.Parse(File.ReadAllText(PIDFile));
                try
                {
                    using (Process PreviousServer = Process.GetProcessById(PreviousPID))
                    using (StreamWriter PreviousInput = PreviousServer.StandardInput)
                    {
                        PreviousInput.WriteLine("/quit");
                        //Make sure the server process has stopped
                        while (PreviousServer.HasExited == false) ;
                    }
                    Wrapper.WriteLine("Previous server stopped successfully");
                }
                catch (Exception)
                {
                    Wrapper.WriteLine("No previous server process running");
                }
                File.Delete(PIDFile);
            }

            /*=======================================
            Configure the server process before starting it
            =======================================*/
            Wrapper.WriteLine("Configuring server process...");
            FactorioServer.Process = new Process();
            FactorioServer.Process.StartInfo.FileName = s.Read("ExePath", "Factorio");
            FactorioServer.Process.StartInfo.Arguments = s.BigRead("ServerArgs", "Factorio");
            FactorioServer.Process.StartInfo.CreateNoWindow = true;
            FactorioServer.Process.StartInfo.ErrorDialog = false;
            FactorioServer.Process.StartInfo.UseShellExecute = false;
            FactorioServer.Process.StartInfo.RedirectStandardError = true;
            FactorioServer.Process.StartInfo.RedirectStandardOutput = true;
            FactorioServer.Process.StartInfo.RedirectStandardInput = true;

            /*=======================================
            These are what read/print/process the server log.
            They'll be run whenever the server process outputs text,
            even while the code continues running below.
            =======================================*/
            Wrapper.WriteLine("Configuring console output...");
            FactorioServer.Process.OutputDataReceived += new DataReceivedEventHandler
            (
                (sender, OutputText) => {
                    if (string.IsNullOrWhiteSpace(OutputText.Data) == false) {
                        if (!FactorioServer.NoConsole) {
                            Util.WriteToLog(OutputText.Data, FactorioServer.OutputFormat);
                        }
                        if (FactorioServer.Stopping == true) {
                            FactorioServer.Loaded = !OutputText.Data.Contains("changing state from(Disconnected) to(Closed)");
                            FactorioServer.Running = !OutputText.Data.EndsWith("Goodbye");
                        }
                        else if (!FactorioServer.Loaded) {
                            FactorioServer.Loaded = OutputText.Data.Contains("changing state from(CreatingGame) to(InGame)");
                        }
                    }
                }
            );
            FactorioServer.Process.ErrorDataReceived += new DataReceivedEventHandler
            (
                (sender, ErrorText) => {
                    if (string.IsNullOrWhiteSpace(ErrorText.Data) == false) {
                        if (!FactorioServer.NoConsole) {
                            Util.WriteToLog(ErrorText.Data, FactorioServer.ErrorFormat);
                        }
                    }
                }
            );

            /*=======================================
            Finally start the dang server process
            =======================================*/
            Wrapper.WriteLine("Starting Factorio server...");
            FactorioServer.NoConsole = true;
            FreeConsole();
            FactorioServer.Process.Start();

            File.WriteAllText(PIDFile, FactorioServer.Process.Id.ToString());

            //Redirect the input so that the code can send text
            FactorioServer.Input = FactorioServer.Process.StandardInput;

            //Start checking for output
            FactorioServer.Process.BeginOutputReadLine();
            FactorioServer.Process.BeginErrorReadLine();

            //Don't try to do anything else until the server finishes loading
            while (FactorioServer.Loaded == false) ;
            AllocConsole();
            FactorioServer.NoConsole = false;
            Console.Title = "Wrapper." + Wrapper.Version + " FactorioServer." + s.Read("Version", "Factorio");
            Wrapper.WriteLine("Factorio server loaded!");

            FactorioServer.Running = true;
            if (!FactorioServer.IsBridge) {
                Wrapper.Command("InputMode 2");
                Wrapper.WriteLine("Use \"wrapper InputMode 0\" to access Wrapper mode.");

                /*=======================================
                This loop monitors for user input in the console
                and sends it to the appropriate process
                =======================================*/
                string ConsoleInput = "";
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
                                case Wrapper.Modes.Menu:
                                    Wrapper.Command(ConsoleInput);
                                    break;
                                //Default to Factorio server
                                case Wrapper.Modes.FactorioServer:
                                    FactorioServer.ProcessInput(ConsoleInput);
                                    break;
                                default:
                                    Wrapper.InputTarget = Wrapper.Modes.Menu;
                                    throw new TrashMonkeyException("Invalid input mode! Defaulting to wrapper mode.");
                            }
                        }
                    }
                    catch (TrashMonkeyException e)
                    { //Handled errors
                        Wrapper.ErrorWriteLine(e.Message);
                    }
                    catch (Exception e)
                    {            //Something actually broke errors
                        Util.PrintErrorInfo(e);
                    }
                    ConsoleInput = "";
                } while (FactorioServer.Running == true);
                //Exiting this loop should return to the menu
            }
        }

        public static void ProcessInput(string InputText) {
            //Send it to the wrapper if it's a wrapper command
            if (InputText.StartsWith("wrapper"))
            {
                Wrapper.Command(InputText.Remove(0, 8));
            }
            else if (InputText == "/quit")
            {
                FactorioServer.StopRoutine();
            }
            //Send it to the Factorio server
            else
            {
                FactorioServer.Input.WriteLine(InputText);
            }
        }

        /*=======================================
        Saves the world and stops the server
        =======================================*/
        public static void StopRoutine() {
            FactorioServer.Stopping = true;
            FactorioServer.Input.WriteLine("/quit");
            //Make sure the server process has stopped
            while (FactorioServer.Loaded == true) ;
            FactorioServer.Input.WriteLine("e");
            while (FactorioServer.Running == true) ;
            if (!FactorioServer.IsBridge) {
                Wrapper.Mode = Wrapper.Modes.Menu;
            }
            FactorioServer.Input = null;
            if (File.Exists(PIDFile))
            {
                File.Delete(PIDFile);
            }
            Wrapper.WriteLine("Server stopped successfully!");
        }
    }
}
