using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    public class LClosure
    {
        internal Proto Proto;
        internal TypedValue[][] UpvalLists;
    }
}
