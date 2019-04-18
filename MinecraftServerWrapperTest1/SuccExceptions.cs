using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/*
    Apparently C# will let you just casually create your own exception types. XD
    This was the template Microsoft's documentation said to use for custom exceptions, so I'm just
    copying it and assuming it works.
*/

namespace SuccExceptions
{
    public class TrashMonkeyException : Exception
    {
        public TrashMonkeyException()
        {
        }

        public TrashMonkeyException(string message)
            : base(message)
        {
        }

        public TrashMonkeyException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
    public class SaveStopException : Exception
    {
        public SaveStopException()
        {
        }

        public SaveStopException(string message)
            : base(message)
        {
        }

        public SaveStopException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
    public class MinecraftInput : Exception
    {
        public MinecraftInput()
        {
        }

        public MinecraftInput(string message)
            : base(message)
        {
        }

        public MinecraftInput(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
