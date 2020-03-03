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
    sealed class NewMinecraftServer : AbstractServer
    {
        public static readonly Util.LogFormat OutputFormat = new Util.LogFormat("[Minecraft] ", ConsoleColor.Green);
        public static readonly Util.LogFormat ErrorFormat = new Util.LogFormat("[Minecraft Error] ", ConsoleColor.DarkGreen);

        public NewMinecraftServer(string ServerName) : base(ServerName) { }

        public override int Run()
        {
            Loaded = false;
            Stopping = false;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Wrapper.InputTarget = Wrapper.Modes.Menu;

            Wrapper.WriteLine("Preparing Minecraft server...");
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
            Console.Title = "Wrapper." + Wrapper.Version + " this." + s.Read("Version", "Minecraft");
            RootPath = s.Read("ServerFolder", "Windows") + '\\';

            //Automatically accept the EULA because screw that
            File.WriteAllText(RootPath + "eula.txt", "eula=TRUE");

            /*=======================================
            Build a huge string of startup arguments from the settings file
            =======================================*/
            //string MinecraftJar = "minecraft_server." + s.Read("Version", "Minecraft") + ".jar";
            string MinecraftJar = s.Read("JarName", "Minecraft");
            string ArgumentsString = "-Xmx" + s.Read("MemMax", "Java") + " -Xms" + s.Read("MemMin", "Java") +
                                " " + s.Read("Type", "Java") + " " + s.BigRead("Arguments", "Java") +
                                " -jar \"" + RootPath;
            if (Convert.ToBoolean(s.Read("Enable", "Fabric")))
            {
                Console.Title = Console.Title + " FabricLoader." + s.Read("Version", "Fabric");
                //string FabricJar = "fabric-loader-" + s.Read("Version", "Fabric") + ".jar";
                string FabricJar = s.Read("FabricJarName", "Fabric");
                ArgumentsString += FabricJar + "\" \"" + RootPath;
                File.WriteAllText(RootPath + "fabric-server-launcher.properties", "serverJar=" + MinecraftJar);
            }


            /*=======================================
            Read the world select file and add stuff
            from that to the end of the arguments string
            =======================================*/
            Wrapper.WriteLine("Configuring universe...");
            string WorldSelectFilePath = RootPath + s.Read("WorldSelectFile", "Minecraft");
            if (!File.Exists(WorldSelectFilePath))
            {
                //dw = DefaultWorld
                IniFile dw = new IniFile(WorldSelectFilePath);
                dw.Write("Selected", "_default", "Universe");
                dw.Write("Selected", "world", "World");
                dw.Write("UniversesFolder", "universes", "Windows");
                dw.Write("RelativePath", "true", "Windows");
            }

            IniFile w = new IniFile(WorldSelectFilePath);

            //The folder path is processed this way since Minecraft prefers a relative path,
            //but the wrapper still needs to access the folder name itself with an absolute path.
            string UniversesFolder = w.Read("UniversesFolderName", "Windows");
            if (Convert.ToBoolean(w.Read("RelativePath", "Windows")))
            {
                UniversesFolder = ".\\" + UniversesFolder;
            }
            if (!UniversesFolder.EndsWith(@"\"))
            {
                UniversesFolder += @"\";
            }

            ArgumentsString += MinecraftJar + "\" " + s.Read("Arguments", "Minecraft") +
                                                " --universe " + UniversesFolder + w.Read("Selected", "Universe") +
                                                " --world " + w.Read("Selected", "World");

            /*=======================================
            Set up server.properties
            =======================================*/
            Wrapper.WriteLine("Configuring server.properties...");
            Dictionary<string, string> ServerProperties = new Dictionary<string, string>();

            //This is just here in case a property manages to
            //not get specified in any other properties file.
            Util.MergeDictionaryWithStream(ServerProperties, "BeeMovieHentai", "ServerWrapperTest.default_server.properties");

            //Loads the defaults for the whole server.
            Util.MergeDictionaryWithStream(ServerProperties, RootPath + s.Read("GlobalServerPropertiesFile", "Minecraft"));

            //Configures the generic template for 
            UniversePath = RootPath + w.Read("UniversesFolderName", "Windows") + "\\" + w.Read("Selected", "Universe") + "\\";
            Util.MergeDictionaryWithStream(ServerProperties, UniversePath + "universe.properties");

            //Update the MotD to show what universe/world is loaded
            ServerProperties["motd"] += " | " + w.Read("Selected", "World");

            //Check for a world properties file and load that too if it exists
            WorldPath = UniversePath + w.Read("Selected", "World") + "\\";
            Util.MergeDictionaryWithStream(ServerProperties, WorldPath + "world.properties");

            ServerProperties["level-name"] = w.Read("Selected", "World");

            File.WriteAllLines(RootPath + "server.properties", Util.JoinDictionaryAsArray(ServerProperties, '='));

            /*=======================================
            Set the server icon
            =======================================*/
            Wrapper.WriteLine("Setting server icon...");

            //These pragma statements whats-its just make Visual Studio shut up
            //about these if statements being empty. I did that on purpose as a way
            //of short circuiting the logic involved.
            #pragma warning disable CS0642
            if (Util.SetIcon(WorldPath + "server-icon.png")) ;
            else if (Util.SetIcon(UniversePath + "server-icon.png")) ;
            else if (Util.SetIcon(RootPath + "global-icon.png")) ;
            else if (Util.CompareDefaultIcon(RootPath + "server-icon.png")) ;
            #pragma warning restore CS0642
            else
            {
                using (Stream DefaultIconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ServerWrapperTest.default-icon.png"))
                {
                    byte[] ByteBuffer = new byte[DefaultIconStream.Length];
                    using (MemoryStream memoryStream = new MemoryStream(ByteBuffer))
                    {
                        DefaultIconStream.CopyTo(memoryStream);
                        File.WriteAllBytes(RootPath + "server-icon.png", memoryStream.ToArray());
                    }
                }
            }

            ///*=======================================
            //Setup some files related to logs and crap
            //=======================================*/
            //Wrapper.WriteLine("Configuring custom logging...");
            ////this.CommandLog = new StreamWriter(RootPath + s.Read("CommandLogFile", "Wrapper"), true);
            //Wrapper.WriteLine("Checking for previous unstopped servers...");
            //PIDFile = RootPath + s.Read("PIDFile", "Minecraft");
            //if (File.Exists(PIDFile))
            //{
            //    Int32 PreviousPID = Int32.Parse(File.ReadAllText(PIDFile));
            //    try
            //    {
            //        using (Process PreviousServer = Process.GetProcessById(PreviousPID))
            //        using (StreamWriter PreviousInput = PreviousServer.StandardInput)
            //        {
            //            PreviousInput.WriteLine("stop");
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
            ServerProcess.StartInfo.FileName = s.Read("Executable", "Java");
            ServerProcess.StartInfo.Arguments = ArgumentsString;
            ServerProcess.StartInfo.CreateNoWindow = false;
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
                            Loaded = !OutputText.Data.Contains("[Server thread/INFO]: Saved the game");
                        }
                        //else if (this.Loaded)
                        //{
                        //    ProcessLog(OutputText.Data.Split(Util.RightBracketSplitter, StringSplitOptions.RemoveEmptyEntries));
                        //}
                        else if (!Loaded)
                        {
                            Loaded = OutputText.Data.Contains("[Server thread/INFO]: Done (");
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
            Wrapper.WriteLine("Starting Minecraft server...");
            ServerProcess.Start();

            //File.WriteAllText(PIDFile, this.ServerProcess.Id.ToString());

            //Redirect the input so that the code can send text
            Input = ServerProcess.StandardInput;

            //Start checking for output
            ServerProcess.BeginOutputReadLine();
            ServerProcess.BeginErrorReadLine();

            //Don't try to do anything else until the server finishes loading
            while (Loaded == false) ;
            Wrapper.WriteLine("Minecraft server loaded!");

            Wrapper.Command("InputMode 1");
            Wrapper.WriteLine("Use \"wrapper InputMode 0\" to access Wrapper mode.");

            /*=======================================
            This loop monitors for user input in the console
            and sends it to the appropriate process
            =======================================*/
            string ConsoleInput = "";
            Running = true;
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
                            //Default to Minecraft server
                            case Wrapper.Modes.MinecraftServer:
                                //Send it to the wrapper if it's a wrapper command
                                if (ConsoleInput.StartsWith("wrapper"))
                                {
                                    Wrapper.Command(ConsoleInput.Remove(0, 7));
                                }
                                //If it's a stop command, switch to the stop routine
                                else if (ConsoleInput == "stop")
                                {
                                    StopRoutine();
                                }
                                else if (ConsoleInput == "stop-no-save")
                                {
                                    StopRoutine(false);
                                }
                                //Send it to the Minecraft server
                                else
                                {
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
            } while (Running == true) ;
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
            if (SaveBeforeStopping)
            {
                Input.WriteLine("save-all");
                //Wait for the save to finish
                while (Loaded == true) ;
            }
            Input.WriteLine("stop");
            //Make sure the server process has stopped
            while (ServerProcess.HasExited == false) ;
            Wrapper.Mode = Wrapper.Modes.Menu;
            Input = null;
            //this.CommandLog.Close();
            //if (File.Exists(PIDFile))
            //{
            //    File.Delete(PIDFile);
            //}
            Wrapper.WriteLine("Server stopped successfully!");
        }
    }
}
