using System;
using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using SuccExceptions;

namespace ServerWrapperTest
{
    class Util
    {
        public static readonly Util.LogFormat OutputFormat = new Util.LogFormat("[C#] ", ConsoleColor.White);
        public static readonly Util.LogFormat ErrorFormat = new Util.LogFormat("[C# Error] ", ConsoleColor.Gray);
        
        public static readonly char[] RightBracketSplitter = { ']' };
        public static readonly char[] SpaceSplitter = { ' ' };
        public static readonly char[] EqualsSplitter = { '=' };

        private const int BYTES_TO_READ = sizeof(Int64);

        public struct LogFormat
        {
            public string Header;
            public ConsoleColor Color;

            public LogFormat(string header, ConsoleColor color)
            {
                Header = header;
                Color = color;
            }
        }

        public static void WriteToLog(string Text, LogFormat Format)
        {
            ConsoleColor OldColor = Console.ForegroundColor;
            Console.ForegroundColor = Format.Color;
            Console.WriteLine(Format.Header + Text);
            Console.ForegroundColor = OldColor;
        }

        /*=======================================
        This is the "unhandled errors" printer
        =======================================*/
        public static void PrintErrorInfo(Exception e)
        {
            ConsoleColor OldColor = Console.ForegroundColor;
            Console.ForegroundColor = ErrorFormat.Color;
            Console.WriteLine(ErrorFormat.Header + "Something went wrong. Moving past error.");
            Console.WriteLine(ErrorFormat.Header + "Error info:");
            Console.WriteLine(ErrorFormat.Header + e.Message);
            Console.WriteLine(ErrorFormat.Header + e.InnerException.ToString());
            Console.ForegroundColor = OldColor;
        }

        /*=======================================
        These save a few lines of code up above so
        I don't have to copy it several times
        =======================================*/
        public static void SetInputTarget(Wrapper.Modes NewMode)
        {
            Wrapper.InputTarget = NewMode;
            switch (NewMode)
            {
                case Wrapper.Modes.Menu:
                    Util.WriteToLog("Switching to wrapper mode.", Wrapper.OutputFormat);
                    Console.ForegroundColor = Wrapper.OutputFormat.Color;
                    break;
                case Wrapper.Modes.MinecraftServer:
                    Util.WriteToLog("Switching to Minecraft mode.", Wrapper.OutputFormat);
                    Console.ForegroundColor = MinecraftServer.OutputFormat.Color;
                    break;
            }
        }

        /*=======================================
        This should maintain a list of the server
        properties to be dynamically created.
        =======================================*/
        public static void MergeDictionaryWithStream(Dictionary<string, string> dictionary, string StreamFilePath, string ManifestResourceString = null)
        {
            if (File.Exists(StreamFilePath) || ManifestResourceString != null)
            {
                using (StreamReader PropertiesFile = ManifestResourceString != null ? new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(ManifestResourceString)) : new StreamReader(StreamFilePath))
                {
                    string[] Line = new string[2];
                    while (!PropertiesFile.EndOfStream)
                    {
                        Line[0] = PropertiesFile.ReadLine();
                        if (string.IsNullOrWhiteSpace(Line[0]) == false && !Line[0].StartsWith("[") && !Line[0].StartsWith("#"))
                        {
                            Line = Line[0].Split(Util.EqualsSplitter, 2, StringSplitOptions.None);
                            if (dictionary.ContainsKey(Line[0]))
                            {
                                dictionary[Line[0]] = Line[1];
                            }
                            else
                            {
                                dictionary.Add(Line[0], Line[1]);
                            }
                        }
                    }
                }
            }
        }

        public static string[] JoinDictionaryAsArray(Dictionary<string, string> dictionary, char KeyValueJoiner)
        {
            List<string> Entries = new List<string>();
            foreach (KeyValuePair<string, string> Entry in dictionary)
            {
                Entries.Add(Entry.Key + KeyValueJoiner + Entry.Value);
            }
            return Entries.ToArray();
        }

        /*=======================================
        This method returns a bool as a cheap and
        lazy way of short-circuiting my icon checking
        logic in MinecraftServer.cs
        =======================================*/
        public static bool SetIcon(string IconPath, bool CompareFiles = true)
        {
            //Optionally skip comparison logic
            if (CompareFiles)
            {
                //If this icon doesn't exist, don't try to do anything
                if (!File.Exists(IconPath))
                {
                    return false;
                }
                //If there's already an icon specified...
                if (File.Exists(MinecraftServer.RootPath + "server-icon.png"))
                {
                    //Don't try to copy over it if they're the same
                    //and skip further icon checks
                    if (FilesAreEqual(new FileInfo(MinecraftServer.RootPath + "server-icon.png"), new FileInfo(IconPath)))
                    {
                        return true;
                    }
                }
            }
            //Copy the icon file
            File.Copy(IconPath, MinecraftServer.RootPath + "server-icon.png", true);
            return true;
        }

        /*=======================================
        I copied this off the internet as a way
        of quickly comparing icon files. There's
        no point in repeatedly copying an icon
        multiple times after all.
        =======================================*/
        public static bool FilesAreEqual(FileInfo first, FileInfo second)
        {
            if (first.Length != second.Length)
                return false;

            //if (string.Equals(first.FullName, second.FullName, StringComparison.OrdinalIgnoreCase))
            //    return true;

            int iterations = (int)Math.Ceiling((double)first.Length / BYTES_TO_READ);
            using (FileStream fs1 = first.OpenRead())
            using (FileStream fs2 = second.OpenRead())
            {
                byte[] one = new byte[BYTES_TO_READ];
                byte[] two = new byte[BYTES_TO_READ];

                for (int i = 0; i < iterations; i++)
                {
                    fs1.Read(one, 0, BYTES_TO_READ);
                    fs2.Read(two, 0, BYTES_TO_READ);

                    if (BitConverter.ToInt64(one, 0) != BitConverter.ToInt64(two, 0))
                        return false;
                }
            }
            return false;
        }

        /*=======================================
        Another short circuit comparison, just this
        time against an MD5 checksum of the default icon.
        =======================================*/
        public static bool CompareDefaultIcon(string IconPath)
        {
            if (!File.Exists(IconPath))
            {
                return false;
            }

            byte[] DefaultIconMD5Hash = { 86, 188, 210, 79, 77, 82, 247, 124, 144, 53, 155, 202, 194, 209, 56, 46 };

            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(IconPath))
                {
                    byte[] CompareHash = md5.ComputeHash(stream);
                    for (int i = 0; i < CompareHash.Length; i++)
                    {
                        if (CompareHash[i] != DefaultIconMD5Hash[i])
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }
    }
}
