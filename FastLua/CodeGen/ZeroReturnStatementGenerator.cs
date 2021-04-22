using FastLua.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    internal class ZeroReturnStatementGenerator : StatementGenerator
    {
        public override void Emit(InstructionWriter writer)
        {
            writer.WriteUUU(OpCodes.RET0, 0, 0, 0);
        }
    }
}
