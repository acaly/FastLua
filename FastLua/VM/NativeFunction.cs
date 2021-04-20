using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    public delegate int NativeFunctionDelegate(StackInfo stackInfo, int argSize);
    public delegate ValueTask<int> AsyncNativeFunctionDelegate(StackInfo stackInfo, int argSize);

    //Used as Target object for wrapped functions.
    internal class WrappedNativeFunctionInfo
    {
        public object RawDelegate;
    }
}
