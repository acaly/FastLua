using FastLua.SyntaxTree;
using FastLua.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    internal class GotoStatementGenerator : StatementGenerator
    {
        private readonly LabelStatementSyntaxNode _label;

        public GotoStatementGenerator(LabelStatementSyntaxNode label)
        {
            _label = label;
        }

        public override void Emit(InstructionWriter writer)
        {
            writer.WriteUSx(OpCodes.JMP, 0, 0);
            writer.AddLabelFix(_label, InstructionWriter.FixUSxJump);
        }
    }
}
