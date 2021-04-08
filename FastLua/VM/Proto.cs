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
        public ImmutableArray<(Proto proto, ImmutableArray<int> upvalSlot)> ChildFunctions;
        public SignatureDesc[] SigDesc; //Should be immutable, but VM wants to take ref to it to avoid copying.

        //Sig type of all parameters (vararg as a vararg part).
        public SignatureDesc ParameterSig;
        //The sig of the vararg param (should be a sig with zero fixed length).
        public SignatureDesc VarargSig;

        public int StackSize;

        public int UpvalRegionOffset; //obj only
        public int LocalRegionOffset;
        public int SigRegionOffset;
    }
}
