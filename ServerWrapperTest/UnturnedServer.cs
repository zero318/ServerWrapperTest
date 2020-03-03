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

namespace ServerWrapperTest
{
    class UnturnedServer
    {
        public static readonly Util.LogFormat OutputFormat = new Util.LogFormat("[Unturned] ", ConsoleColor.Magenta);
        public static readonly Util.LogFormat ErrorFormat = new Util.LogFormat("[Unturned Error] ", ConsoleColor.DarkMagenta);

        private static Process Process;

        public static bool Running = false;
        private static bool Loaded;
        //private static bool Stopping;
        private static string PIDFile;

        public static string RootPath;
        //public static string UniversePath;
        //public static string WorldPath;

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
            UnturnedServer.Loaded = false;
            //UnturnedServer.Stopping = false;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Wrapper.InputTarget = Wrapper.Modes.Menu;

            Wrapper.WriteLine("Preparing Unturned server...");
            string SettingsFileName = "Settings.ini";
            string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);

            /*=======================================
            Create a settings file with some default values if it doesn't exist.
            These default values probably won't actually work, but it'll at least create the file.
            =======================================*/
            if (!File.Exists(SettingsFilePath))
            {
                //ds = DefaultSettings
                IniFile ds = new IniFile(SettingsFileName);
                ds.Write("Version", "1.15.1", "Minecraft");
                ds.Write("Arguments", "nogui", "Minecraft");
                ds.Write("WorldSelectFile", "w.ini", "Minecraft");
                ds.Write("Enable", "false", "Fabric");
                ds.Write("Version", "0.4.0+build.121", "Fabric");
                ds.Write("CommandLogFile", "CommandLog.txt", "Wrapper");
                ds.Write("ServerPIDFile", "ServerPID", "Wrapper");
                ds.Write("ServerFolder", @"E:\My_Minecraft_Expansion_2\Local Server", "Windows");
                ds.Write("Executable", "java", "Java");
                ds.Write("Type", "-d64 -server", "Java");
                ds.Write("MemMax", "4G", "Java");
                ds.Write("MemMin", "4G", "Java");
                ds.Write("LogConfigFile", "log4j2.xml", "Java");
                ds.Write("Arguments", "-XX:+UseConcMarkSweepGC -XX:+DisableExplicitGC -XX:+UseAdaptiveGCBoundary -XX:MaxGCPauseMillis=500 -XX:-UseGCOverheadLimit -XX:SurvivorRatio=12 -XX:NewRatio=4 -Xnoclassgc -XX:UseSSE=3", "Java");
            }

            /*=======================================
            Read the settings file to find the server path
            =======================================*/
            Wrapper.WriteLine("Reading wrapper settings file...");
            IniFile s = new IniFile(SettingsFileName);
            Console.Title = "Wrapper." + Wrapper.Version + " UnturnedServer." + s.Read("Version", "Unturned");
            RootPath = s.Read("ServerFolder", "Windows") + '\\';


            /*=======================================
            Setup some files related to logs and crap
            =======================================*/
            Wrapper.WriteLine("Configuring custom logging...");
            UnturnedServer.CommandLog = new StreamWriter(RootPath + s.Read("CommandLogFile", "Wrapper"), true);
            Wrapper.WriteLine("Checking for previous unstopped servers...");
            PIDFile = RootPath + s.Read("PIDFile", "Unturned");
            if (File.Exists(PIDFile))
            {
                Int32 PreviousPID = Int32.Parse(File.ReadAllText(PIDFile));
                try
                {
                    using (Process PreviousServer = Process.GetProcessById(PreviousPID))
                    using (StreamWriter PreviousInput = PreviousServer.StandardInput)
                    {
                        PreviousInput.WriteLine("shutdown");
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
            UnturnedServer.Process = new Process();
            UnturnedServer.Process.StartInfo.FileName = s.Read("ServerFolder", "Unturned");
            UnturnedServer.Process.StartInfo.Arguments = s.Read("Arguments", "Unturned");
            UnturnedServer.Process.StartInfo.CreateNoWindow = true;
            UnturnedServer.Process.StartInfo.WorkingDirectory = RootPath;
            UnturnedServer.Process.StartInfo.ErrorDialog = false;
            UnturnedServer.Process.StartInfo.UseShellExecute = false;
            UnturnedServer.Process.StartInfo.RedirectStandardError = true;
            UnturnedServer.Process.StartInfo.RedirectStandardOutput = true;
            UnturnedServer.Process.StartInfo.RedirectStandardInput = true;


            /*=======================================
            These are what read/print/process the server log.
            They'll be run whenever the server process outputs text,
            even while the code continues running below.
            =======================================*/
            Wrapper.WriteLine("Configuring console output...");
            UnturnedServer.Process.OutputDataReceived += new DataReceivedEventHandler
            (
                (sender, OutputText) =>
                {
                    if (string.IsNullOrWhiteSpace(OutputText.Data) == false)
                    {
                        Util.WriteToLog(OutputText.Data, UnturnedServer.OutputFormat);

                        //if (UnturnedServer.Stopping == true)
                        //{
                        //    UnturnedServer.Loaded = !OutputText.Data.Contains("Successfully saved the game.");
                        //}
                        if (!UnturnedServer.Loaded)
                        {
                            UnturnedServer.Loaded = OutputText.Data.Contains("Loading level: 100%");
                        }
                    }
                }
            );
            UnturnedServer.Process.ErrorDataReceived += new DataReceivedEventHandler
            (
                (sender, ErrorText) =>
                {
                    if (string.IsNullOrWhiteSpace(ErrorText.Data) == false)
                    {
                        Util.WriteToLog(ErrorText.Data, UnturnedServer.ErrorFormat);
                    }
                }
            );

            /*=======================================
            Finally start the dang server process
            =======================================*/
            Wrapper.WriteLine("Starting Unturned server...");
            UnturnedServer.Process.Start();

            File.WriteAllText(PIDFile, UnturnedServer.Process.Id.ToString());

            //Redirect the input so that the code can send text
            UnturnedServer.Input = UnturnedServer.Process.StandardInput;

            //Start checking for output
            UnturnedServer.Process.BeginOutputReadLine();
            UnturnedServer.Process.BeginErrorReadLine();

            //Don't try to do anything else until the server finishes loading
            while (UnturnedServer.Loaded == false) ;
            Wrapper.WriteLine("Unturned server loaded!");

            Wrapper.Command("InputMode 4");
            Wrapper.WriteLine("Use \"wrapper InputMode 0\" to access Wrapper mode.");

            /*=======================================
            This loop monitors for user input in the console
            and sends it to the appropriate process
            =======================================*/
            string ConsoleInput = "";
            UnturnedServer.Running = true;
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
                            //Default to Unturned server
                            case Wrapper.Modes.UnturnedServer:
                                //Send it to the wrapper if it's a wrapper command
                                if (ConsoleInput.StartsWith("wrapper"))
                                {
                                    Wrapper.Command(ConsoleInput.Remove(0, 7));
                                }
                                //If it's a stop command, switch to the stop routine
                                else if (ConsoleInput == "stop")
                                {
                                    UnturnedServer.StopRoutine();
                                }
                                else if (ConsoleInput == "shutdown-no-save")
                                {
                                    UnturnedServer.StopRoutine(false);
                                }
                                //Send it to the Unturned server
                                else
                                {
                                    UnturnedServer.Input.WriteLine(ConsoleInput);
                                }
                                break;
                            default:
                                Wrapper.InputTarget = Wrapper.Modes.Menu;
                                throw new TrashMonkeyException("Invalid input mode! Defaulting to wrapper mode.");
                        }
                    }
                }
                catch (TrashMonkeyException e)  //Handled errors
                {
                    Wrapper.ErrorWriteLine(e.Message);
                }
                catch (Exception e)             //Something actually broke errors
                {
                    Util.PrintErrorInfo(e);
                }
                ConsoleInput = "";
            } while (UnturnedServer.Running == true);
            //Exiting this loop should return to the menu
        }

        /*=======================================
        Saves the world and stops the server
        =======================================*/
        public static void StopRoutine(bool SaveBeforeStopping = true)
        {
            UnturnedServer.Running = false;
            //UnturnedServer.Stopping = true;
            //if (SaveBeforeStopping)
            //{
            //    UnturnedServer.Input.WriteLine("save");
            //    //Wait for the save to finish
            //    while (UnturnedServer.Loaded == true) ;
            //}
            UnturnedServer.Input.WriteLine("shutdown");
            //Make sure the server process has stopped
            while (UnturnedServer.Process.HasExited == false) ;
            Wrapper.Mode = Wrapper.Modes.Menu;
            UnturnedServer.Input = null;
            UnturnedServer.CommandLog.Close();
            if (File.Exists(PIDFile))
            {
                File.Delete(PIDFile);
            }
            Wrapper.WriteLine("Server stopped successfully!");
        }
    }
}
