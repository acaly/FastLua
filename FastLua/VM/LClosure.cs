using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    internal class LClosure
    {
        public Proto Proto;
        public TypedValue[][] UpvalLists;
    }
}
