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
    sealed class NewFactorioServer : AbstractServer
    {
        public static readonly Util.LogFormat OutputFormat = new Util.LogFormat("[Factorio] ", ConsoleColor.Red);
        public static readonly Util.LogFormat ErrorFormat = new Util.LogFormat("[Factorio Error] ", ConsoleColor.DarkRed);

        public NewFactorioServer(string ServerName) : base(ServerName) { }

        public override int Run()
        {
            Loaded = false;
            Stopping = false;
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
                //ds = DefaultSettings
                IniFile ds = new IniFile(SettingsFileName);
                ds.Write("Version", "1.14", "Minecraft");
            }

            /*=======================================
            Read the settings file to find the server path
            =======================================*/
            Wrapper.WriteLine("Reading wrapper settings file...");
            IniFile s = new IniFile(SettingsFileName);
            Console.Title = "Wrapper." + Wrapper.Version + " this." + s.Read("Version", "Factorio");
            RootPath = s.Read("ServerFolder", "Windows") + '\\';

            ///*=======================================
            //Setup some files related to logs and crap
            //=======================================*/
            //Wrapper.WriteLine("Configuring custom logging...");
            //this.CommandLog = new StreamWriter(RootPath + s.Read("CommandLogFile", "Wrapper"), true);
            //Wrapper.WriteLine("Checking for previous unstopped servers...");
            //PIDFile = RootPath + s.Read("PIDFile", "Factorio");
            //if (File.Exists(PIDFile))
            //{
            //    Int32 PreviousPID = Int32.Parse(File.ReadAllText(PIDFile));
            //    try
            //    {
            //        using (Process PreviousServer = Process.GetProcessById(PreviousPID))
            //        using (StreamWriter PreviousInput = PreviousServer.StandardInput)
            //        {
            //            PreviousInput.WriteLine("/quit");
            //            //Make sure the server process has stopped
            //            while (PreviousServer.HasExited == false) ;
            //        }
            //        Wrapper.WriteLine("Previous server stopped successfully");
            //    }
            //    catch (Exception)
            //    {
            //        Wrapper.WriteLine("No previous server process running");
            //    }
            //    File.Delete(PIDFile);
            //}

            /*=======================================
            Configure the server process before starting it
            =======================================*/
            Wrapper.WriteLine("Configuring server process...");
            ServerProcess = new Process();
            ServerProcess.StartInfo.FileName = s.Read("ServerFolder", "Factorio");
            ServerProcess.StartInfo.Arguments = s.Read("Arguments", "Factorio");
            ServerProcess.StartInfo.CreateNoWindow = true;
            ServerProcess.StartInfo.WorkingDirectory = RootPath;
            ServerProcess.StartInfo.ErrorDialog = false;
            ServerProcess.StartInfo.UseShellExecute = false;
            ServerProcess.StartInfo.RedirectStandardError = true;
            ServerProcess.StartInfo.RedirectStandardOutput = true;
            ServerProcess.StartInfo.RedirectStandardInput = true;


            /*=======================================
            These are what read/print/process the server log.
            They'll be run whenever the server process outputs text,
            even while the code continues running below.
            =======================================*/
            Wrapper.WriteLine("Configuring console output...");
            ServerProcess.OutputDataReceived += new DataReceivedEventHandler
            (
                (sender, OutputText) =>
                {
                    if (string.IsNullOrWhiteSpace(OutputText.Data) == false)
                    {
                        Util.WriteToLog(OutputText.Data, OutputFormat);

                        if (Stopping == true)
                        {
                            Loaded = !OutputText.Data.Contains("changing state from(Disconnected) to(Closed)");
                        }
                        else if (!Loaded)
                        {
                            Loaded = OutputText.Data.Contains("changing state from(CreatingGame) to(InGame)");
                        }
                    }
                }
            );
            ServerProcess.ErrorDataReceived += new DataReceivedEventHandler
            (
                (sender, ErrorText) =>
                {
                    if (string.IsNullOrWhiteSpace(ErrorText.Data) == false)
                    {
                        Util.WriteToLog(ErrorText.Data, ErrorFormat);
                    }
                }
            );

            /*=======================================
            Finally start the dang server process
            =======================================*/
            Wrapper.WriteLine("Starting Factorio server...");
            ServerProcess.Start();

            //File.WriteAllText(PIDFile, this.Process.Id.ToString());

            //Redirect the input so that the code can send text
            Input = ServerProcess.StandardInput;

            //Start checking for output
            ServerProcess.BeginOutputReadLine();
            ServerProcess.BeginErrorReadLine();

            //Don't try to do anything else until the server finishes loading
            while (Loaded == false) ;
            Wrapper.WriteLine("Factorio server loaded!");

            Wrapper.Command("InputMode 2");
            Wrapper.WriteLine("Use \"wrapper InputMode 0\" to access Wrapper mode.");

            /*=======================================
            This loop monitors for user input in the console
            and sends it to the appropriate process
            =======================================*/
            string ConsoleInput = "";
            //string ConsoleInput = Console.In.ReadLineAsync().;
            Running = true;
            //this.Process.
            do
            {
                try
                {
                    //Get user input
                    ConsoleInput = Console.In.ReadLine();
                    //ConsoleInput = Console.In.ReadLineAsync().Result;

                    //If the user wasn't a squit
                    if (string.IsNullOrWhiteSpace(ConsoleInput) == false)
                    {
                        switch (Wrapper.InputTarget)
                        {
                            //Default to wrapper
                            case Wrapper.Modes.Menu:
                                Wrapper.Command(ConsoleInput);
                                break;
                            //Default to Minecraft server
                            case Wrapper.Modes.FactorioServer:
                                //Send it to the wrapper if it's a wrapper command
                                if (ConsoleInput.StartsWith("wrapper"))
                                {
                                    Wrapper.Command(ConsoleInput.Remove(0, 7));
                                }
                                //If it's a stop command, switch to the stop routine
                                else if (ConsoleInput == "/stop")
                                {
                                    StopRoutine();
                                }
                                //else if (ConsoleInput == "stop-no-save")
                                //{
                                //    this.StopRoutine(false);
                                //}
                                //Send it to the Minecraft server
                                else
                                {
                                    //this.Process.StandardInput.WriteLine(ConsoleInput);
                                    Input.WriteLine(ConsoleInput);
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
            } while (Running == true);
            //Exiting this loop should return to the menu
            return 1;
        }

        /*=======================================
        Saves the world and stops the server
        =======================================*/
        public override void StopRoutine(bool SaveBeforeStopping = true)
        {
            Running = false;
            Stopping = true;
            //if (SaveBeforeStopping)
            //{
            //    this.Input.WriteLine("save-all");
            //    //Wait for the save to finish
            //    while (this.Loaded == true) ;
            //}
            Input.WriteLine("/quit");
            //this.Process.StandardInput.WriteLine("/quit");
            //Make sure the server process has stopped
            while (ServerProcess.HasExited == false) ;
            //while (this.Loaded == true) ;
            Wrapper.Mode = Wrapper.Modes.Menu;
            Input = null;
            //CommandLog.Close();
            //if (File.Exists(PIDFile))
            //{
            //    File.Delete(PIDFile);
            //}
            Wrapper.WriteLine("Server stopped successfully!");
        }
    }
}
