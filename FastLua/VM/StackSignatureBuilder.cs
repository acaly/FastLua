using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    //Used by StackSignature and CodeGen's SequentialStackLayout. This is provided
    //to keep both to be consistent, with each other.
    //Note that this is a mutable struct. Use ref if passed around.
    //Note: when supporting vararg part, the vararg (even when specialized) should
    //start at StackSignatureBuilder.Length. This ensures the vararg is always
    //at aligned position. This simplifies the stack allocation for invocations and
    //the sig adjustment in RETN and CALL/CALLC.
    //Also see comment in InvocationExpressionGenerator.
    internal struct StackSignatureBuilder
    {
        private int _obj, _num;

        public int AddObject()
        {
            return _obj++;
        }

        public int AddNumber()
        {
            return _num++;
        }

        public int AddUnspecialized()
        {
            var ret = Length;
            _obj = _num = ret + 1;
            return ret;
        }

        public int Length => Math.Max(_obj, _num);
    }
}
