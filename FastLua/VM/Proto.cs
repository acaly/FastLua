using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    internal class Proto
    {
        public ImmutableArray<uint> Instructions;
        public ImmutableArray<TypedValue> Constants;
        public ImmutableArray<Proto> ChildFunctions;
        public SignatureDesc[] SigDesc; //Should be immutable, but VM wants to take ref to it to avoid copying.

        public SignatureDesc ParameterSig;

        public int StackSize;

        public int UpvalRegionOffset; //obj only
        public int LocalRegionOffset;
        public int SigRegionOffset;
    }
}
