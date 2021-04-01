using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    internal class Thread
    {
        public StackManager Stack = new();
        public StringBuilder StringBuilder = new();
        public List<TypedValue> VarargStack = new();
        public int VarargTotalLength = 0;
    }
}
