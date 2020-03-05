using System;
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

        [DllImport("kernel32")]
        static extern bool AllocConsole();

        public static readonly Util.LogFormat OutputFormat = new Util.LogFormat("[Factorio] ", ConsoleColor.Red);
        public static readonly Util.LogFormat ErrorFormat = new Util.LogFormat("[Factorio Error] ", ConsoleColor.DarkRed);

        private static Process Process;

        public static bool Running = false;
        private static bool Loaded;
        private static bool Stopping;

        /*=======================================
        This subroutine starts up the server
        and then monitors the output.
        =======================================*/
        public static void Run() {
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
            
            /*=======================================
            Configure the server process before starting it
            =======================================*/
            Wrapper.WriteLine("Configuring server process...");
            FactorioServer.Process = new Process();
            FactorioServer.Process.StartInfo.FileName = s.Read("ExePath", "Factorio");
            FactorioServer.Process.StartInfo.Arguments = s.Read("ServerArgs", "Factorio");
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
                        if (FactorioServer.Loaded) {
                            Util.WriteToLog(OutputText.Data, FactorioServer.OutputFormat);
                        }
                        else {
                            
                        }
                        if (FactorioServer.Stopping == true) {
                            FactorioServer.Loaded = !OutputText.Data.Contains("changing state from(Disconnected) to(Closed)");
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
                        if (FactorioServer.Loaded) {
                            Util.WriteToLog(ErrorText.Data, FactorioServer.ErrorFormat);
                        }
                        else {
                            
                        }
                    }
                }
            );

            /*=======================================
            Finally start the dang server process
            =======================================*/
            Wrapper.WriteLine("Starting Factorio server...");
            FreeConsole();
            FactorioServer.Process.Start();

            //Start checking for output
            FactorioServer.Process.BeginOutputReadLine();
            FactorioServer.Process.BeginErrorReadLine();

            //Don't try to do anything else until the server finishes loading
            while (FactorioServer.Loaded == false) ;
            AllocConsole();
            Wrapper.WriteLine("Factorio server loaded!");

            Wrapper.Command("InputMode 2");
            Wrapper.WriteLine("Use \"wrapper InputMode 0\" to access Wrapper mode.");

            /*=======================================
            This loop monitors for user input in the console
            and sends it to the appropriate process
            =======================================*/
            string ConsoleInput = "";
            FactorioServer.Running = true;
            do {
                try {
                    //Get user input
                    ConsoleInput = Console.In.ReadLine();

                    //If the user wasn't a squit
                    if (string.IsNullOrWhiteSpace(ConsoleInput) == false) {
                        switch (Wrapper.InputTarget) {
                            //Default to wrapper
                            case Wrapper.Modes.Menu:
                                Wrapper.Command(ConsoleInput);
                                break;
                            //Default to Factorio server
                            case Wrapper.Modes.FactorioServer:
                                //Send it to the wrapper if it's a wrapper command
                                if (ConsoleInput.StartsWith("wrapper")) {
                                    Wrapper.Command(ConsoleInput.Remove(0, 7));
                                }
                                //Send it to the Factorio server
                                else {
                                    //This line doesn't actually do anything...?
                                    //https://forums.factorio.com/viewtopic.php?f=58&t=75627
                                    FactorioServer.Process.StandardInput.WriteLine(ConsoleInput);
                                }
                                break;
                            default:
                                Wrapper.InputTarget = Wrapper.Modes.Menu;
                                throw new TrashMonkeyException("Invalid input mode! Defaulting to wrapper mode.");
                        }
                    }
                }
                catch (TrashMonkeyException e) { //Handled errors
                    Wrapper.ErrorWriteLine(e.Message);
                }
                catch (Exception e) {            //Something actually broke errors
                    Util.PrintErrorInfo(e);
                }
                ConsoleInput = "";
            } while (FactorioServer.Running == true);
            //Exiting this loop should return to the menu
        }

        /*=======================================
        Saves the world and stops the server

        I don't actually know if this section works yet since
        the weird input behavior has always occured before
        this code is even reached. Can't really test/debug it.
        =======================================*/
        public static void StopRoutine() {
            FactorioServer.Running = false;
            FactorioServer.Stopping = true;
            FactorioServer.Process.StandardInput.WriteLine("/quit");
            //Make sure the server process has stopped
            while (FactorioServer.Process.HasExited == false) ;
            //while (FactorioServer.Loaded == true) ;
            Wrapper.Mode = Wrapper.Modes.Menu;
            Wrapper.WriteLine("Server stopped successfully!");
        }
    }
}
