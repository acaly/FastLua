using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua
{
    public static class Options
    {
        //Currently no instructions to access outside 256 slots, so keep this smaller than 256.
        public static int MaxSingleFunctionStackSize = 200;
        public static int StackSegmentSize = 4000;
    }
}
