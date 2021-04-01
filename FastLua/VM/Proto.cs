using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    internal class Proto
    {
        public uint[] Instructions;
        public TypedValue[] ConstantsU;
        public Proto[] ChildFunctions;
        public SignatureDesc[] SigDesc;

        public SignatureDesc ParameterSig;

        public int NumStackSize;
        public int ObjStackSize;

        public int UpvalRegionOffset; //obj only
        public int LocalRegionOffsetO, LocalRegionOffsetV;
        public int SigRegionOffsetO, SigRegionOffsetV;
    }
}
