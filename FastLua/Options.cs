using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("FastLuaTest")]

namespace FastLua
{
    public static class Options
    {
        //Currently no instructions to access outside 256 slots, so keep this smaller than 256.
        public static int MaxSingleFunctionStackSize = 200;
        public static int StackSegmentSize = 4000;
        public static int InitialFrameCapacity = 100;
        public static int DefaultNativeStackSize = 20;
    }
}
