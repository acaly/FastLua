using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    internal class StackSignature
    {
        public ulong GlobalId;
        public VMSpecializationType[] Fixed;
        public VMSpecializationType? Vararg;
    }
}
