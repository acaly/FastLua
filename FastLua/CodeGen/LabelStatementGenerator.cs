using FastLua.SyntaxTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    internal class LabelStatementGenerator : StatementGenerator
    {
        private readonly LabelStatementSyntaxNode _label;

        public LabelStatementGenerator(LabelStatementSyntaxNode label)
        {
            _label = label;
        }

        public override void Emit(InstructionWriter writer)
        {
            writer.MarkLabel(_label);
        }
    }
}
