using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    //TODO need to have API to access stack
    public delegate int NativeFunctionDelegate();

    internal class NClosure
    {
        public NativeFunctionDelegate Delegate;
        public TypedValue[][] UpvalLists;
    }
}
