using System;
using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using SuccExceptions;

namespace ServerWrapperTest {
    sealed class Util {
        public static readonly Util.LogFormat OutputFormat = new Util.LogFormat("[C#] ", ConsoleColor.White);
        public static readonly Util.LogFormat ErrorFormat = new Util.LogFormat("[C# Error] ", ConsoleColor.Gray);
        
        public static readonly char[] RightBracketSplitter = { ']' };
        public static readonly char[] SpaceSplitter = { ' ' };
        public static readonly char[] EqualsSplitter = { '=' };
        public static readonly char[] AsteriskSplitter = { '*' };

        private const int BYTES_TO_READ = sizeof(Int64);

        public struct LogFormat {
            public string Header;
            public ConsoleColor Color;

            public LogFormat(string header, ConsoleColor color) {
                Header = header;
                Color = color;
            }
        }

        public static void WriteToLog(string Text, LogFormat Format) {
            ConsoleColor OldColor = Console.ForegroundColor;
            Console.ForegroundColor = Format.Color;
            Console.WriteLine(Format.Header + Text);
            Console.ForegroundColor = OldColor;
        }

        /*=======================================
        This is the "unhandled errors" printer
        =======================================*/
        public static void PrintErrorInfo(Exception e) {
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
        public static void SetInputTarget(Wrapper.Modes NewMode) {
            Wrapper.InputTarget = NewMode;
            switch (NewMode) {
                case Wrapper.Modes.Menu:
                    Util.WriteToLog("Switching to wrapper mode.", Wrapper.OutputFormat);
                    Console.ForegroundColor = Wrapper.OutputFormat.Color;
                    break;
                case Wrapper.Modes.FactorioServer:
                    Util.WriteToLog("Switching to Factorio mode.", Wrapper.OutputFormat);
                    Console.ForegroundColor = FactorioServer.OutputFormat.Color;
                    break;
            }
        }
    }
}
