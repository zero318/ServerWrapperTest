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
    public abstract class AbstractServer
    {
        //internal Util.LogFormat OutputFormat;
        //internal Util.LogFormat ErrorFormat;
        //internal Wrapper.Modes ServerType;

        protected Process ServerProcess;

        public string Name;

        public bool Running;
        protected bool Loaded;
        protected bool Stopping;

        public string RootPath;
        public string UniversePath;
        public string WorldPath;

        //I moved this up here since I was having errors accessing it in subroutines.
        //I'm not sure what the proper way of doing it is, but this'll work.
        public StreamWriter Input;
        //private StreamWriter CommandLog;

        protected internal AbstractServer(string ServerName)
        {
            Name = ServerName;
        }

        public abstract int Run();

        public abstract void StopRoutine(bool SaveBeforeStopping = true);
    }
}
