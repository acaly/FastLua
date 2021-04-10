using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    internal class Table
    {
        public TypedValue Get(TypedValue key)
        {
            throw new NotImplementedException();
        }

        public void Set(TypedValue key, TypedValue val)
        {
            throw new NotImplementedException();
        }

        public void SetSequence(ReadOnlySpan<TypedValue> stack, ref SignatureDesc sigDesc)
        {
            throw new NotImplementedException();
        }
    }
}
