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
            return Math.Max(_obj++, _num++);
        }

        public int Length => Math.Max(_obj, _num);
    }
}
