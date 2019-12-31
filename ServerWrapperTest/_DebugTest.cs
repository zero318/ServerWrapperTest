using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SuccExceptions;
using fNbt;

/*=======================================
This file is merely for testing code to see if it works.
It should not be used for anything intended to run normally.
=======================================*/

namespace ServerWrapperTest
{
    class _DebugTest
    {
        private const string TestPath = @"E:\My_Minecraft_Expansion_2\Local Server\universes\survival\Superhappyfuntime\";

        public static void Test()
        {
            fNbt.NbtFile test = new fNbt.NbtFile(TestPath + "level.dat");
            long Seed;

            Seed = test.RootTag.Get("Data")["RandomSeed"].LongValue;
        }
    }
}
