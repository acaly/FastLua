using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    internal abstract class StatementGenerator
    {
        public abstract void Emit(InstructionWriter writer);
    }
}
