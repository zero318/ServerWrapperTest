using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using Ini;
using SuccExceptions;

/*=======================================
This file is a placeholder! It doesn't do anything yet!
=======================================*/

namespace ServerWrapperTest
{
    class FactorioServer
    {
        public static readonly Util.LogFormat OutputFormat = new Util.LogFormat("[Factorio] ", ConsoleColor.Red);
        public static readonly Util.LogFormat ErrorFormat = new Util.LogFormat("[Factorio Error] ", ConsoleColor.DarkRed);

        private static Process Process;

        public static bool Running = false;
        private static bool Loaded;
        private static bool Stopping;
        private static string PIDFile;

        public static string RootPath;

        //I moved this up here since I was having errors accessing it in subroutines.
        //I'm not sure what the proper way of doing it is, but this'll work.
        public static StreamWriter Input;
        private static StreamWriter CommandLog;

        /*=======================================
        This subroutine starts up the server
        and then monitors the output.
        =======================================*/
        public static void Run()
        {
            FactorioServer.Loaded = false;
            FactorioServer.Stopping = false;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Wrapper.InputTarget = Wrapper.Modes.Menu;

            Wrapper.WriteLine("Preparing Factorio server...");
            string SettingsFileName = "Settings.ini";
            string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);

            /*=======================================
            Create a settings file with some default values if it doesn't exist.
            These default values probably won't actually work, but it'll at least create the file.
            =======================================*/
            if (!File.Exists(SettingsFilePath))
            {
                IniFile ds = new IniFile(SettingsFileName);
                ds.Write("Version", "1.14", "Minecraft");
            }

            /*=======================================
            Read the settings file to find the server path
            =======================================*/
            Wrapper.WriteLine("Reading wrapper settings file...");
            IniFile s = new IniFile(SettingsFileName);
            Console.Title = "Wrapper." + Wrapper.Version + " FactorioServer." + s.Read("Version", "Factorio");
            RootPath = s.Read("ServerFolder", "Windows") + '\\';

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
            FactorioServer.Process.StartInfo.Arguments = s.Read("ServerArgs", "Factorio");
            FactorioServer.Process.StartInfo.CreateNoWindow = true;
            FactorioServer.Process.StartInfo.WorkingDirectory = RootPath;
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
                (sender, OutputText) =>
                {
                    if (string.IsNullOrWhiteSpace(OutputText.Data) == false)
                    {
                        Util.WriteToLog(OutputText.Data, FactorioServer.OutputFormat);

                        if (FactorioServer.Stopping == true)
                        {
                            FactorioServer.Loaded = !OutputText.Data.Contains("changing state from(Disconnected) to(Closed)");
                        }
                        else if (!FactorioServer.Loaded)
                        {
                            FactorioServer.Loaded = OutputText.Data.Contains("changing state from(CreatingGame) to(InGame)");
                        }
                    }
                }
            );
            FactorioServer.Process.ErrorDataReceived += new DataReceivedEventHandler
            (
                (sender, ErrorText) =>
                {
                    if (string.IsNullOrWhiteSpace(ErrorText.Data) == false)
                    {
                        Util.WriteToLog(ErrorText.Data, FactorioServer.ErrorFormat);
                    }
                }
            );

            /*=======================================
            Finally start the dang server process
            =======================================*/
            Wrapper.WriteLine("Starting Factorio server...");
            FactorioServer.Process.Start();

            File.WriteAllText(PIDFile, FactorioServer.Process.Id.ToString());

            //Redirect the input so that the code can send text
            FactorioServer.Input = FactorioServer.Process.StandardInput;

            //Start checking for output
            FactorioServer.Process.BeginOutputReadLine();
            FactorioServer.Process.BeginErrorReadLine();

            //Don't try to do anything else until the server finishes loading
            while (FactorioServer.Loaded == false) ;
            Wrapper.WriteLine("Factorio server loaded!");

            Wrapper.Command("InputMode 2");
            Wrapper.WriteLine("Use \"wrapper InputMode 0\" to access Wrapper mode.");
            FactorioServer.Process.WaitForExit();

            /*=======================================
            This loop monitors for user input in the console
            and sends it to the appropriate process
            =======================================*/
            string ConsoleInput = "";
            //string ConsoleInput = Console.In.ReadLineAsync().;
            FactorioServer.Running = true;
            //FactorioServer.Process.
            do
            {
                try
                {
                    //Get user input
                    ConsoleInput = Console.ReadLine();
                    ConsoleInput = Console.ReadLine();
                    //Console.In.W
                    //ConsoleInput = Console.In.ReadLine();
                    FactorioServer.Input.WriteLine("test");
                    //ConsoleInput = Console.In.ReadLineAsync().Result;

                    //If the user wasn't a squit
                    //if (string.IsNullOrWhiteSpace(ConsoleInput) == false)
                    //{
                    //    switch (Wrapper.InputTarget)
                    //    {
                    //        //Default to wrapper
                    //        case Wrapper.Modes.Menu:
                    //            Wrapper.Command(ConsoleInput);
                    //            break;
                    //        //Default to Minecraft server
                    //        case Wrapper.Modes.FactorioServer:
                    //            //Send it to the wrapper if it's a wrapper command
                    //            if (ConsoleInput.StartsWith("wrapper"))
                    //            {
                    //                Wrapper.Command(ConsoleInput.Remove(0, 7));
                    //            }
                    //            //If it's a stop command, switch to the stop routine
                    //            else if (ConsoleInput == "/stop")
                    //            {
                    //                FactorioServer.StopRoutine();
                    //            }
                    //            //else if (ConsoleInput == "stop-no-save")
                    //            //{
                    //            //    FactorioServer.StopRoutine(false);
                    //            //}
                    //            //Send it to the Minecraft server
                    //            else
                    //            {
                    //                //FactorioServer.Process.StandardInput.WriteLine(ConsoleInput);
                    //                FactorioServer.Input.WriteLine(ConsoleInput);
                    //            }
                    //            break;
                    //        default:
                    //            Wrapper.InputTarget = Wrapper.Modes.Menu;
                    //            throw new TrashMonkeyException("Invalid input mode! Defaulting to wrapper mode.");
                    //    }
                    //}
                }
                catch (TrashMonkeyException e)  //Handled errors
                {
                    Wrapper.ErrorWriteLine(e.Message);
                }
                catch (Exception e)             //Something actually broke errors
                {
                    Util.PrintErrorInfo(e);
                }
                //ConsoleInput = "";
            } while (FactorioServer.Running == true);
            //Exiting this loop should return to the menu
        }

        /*=======================================
        Saves the world and stops the server
        =======================================*/
        public static void StopRoutine(bool SaveBeforeStopping = true)
        {
            FactorioServer.Running = false;
            FactorioServer.Stopping = true;
            //if (SaveBeforeStopping)
            //{
            //    FactorioServer.Input.WriteLine("save-all");
            //    //Wait for the save to finish
            //    while (FactorioServer.Loaded == true) ;
            //}
            FactorioServer.Input.WriteLine("/quit");
            //FactorioServer.Process.StandardInput.WriteLine("/quit");
            //Make sure the server process has stopped
            while (FactorioServer.Process.HasExited == false) ;
            //while (FactorioServer.Loaded == true) ;
            Wrapper.Mode = Wrapper.Modes.Menu;
            FactorioServer.Input = null;
            if (File.Exists(PIDFile))
            {
                File.Delete(PIDFile);
            }
            Wrapper.WriteLine("Server stopped successfully!");
        }
    }
}
