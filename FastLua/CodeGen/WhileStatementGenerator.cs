using FastLua.SyntaxTree;
using FastLua.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    internal sealed class WhileStatementGenerator : StatementGenerator
    {
        private readonly ConditionGenerator _cond;
        private readonly StatementGenerator _block;
        private readonly LabelStatementSyntaxNode _exitLabel;

        public WhileStatementGenerator(GeneratorFactory factory, BlockGenerator block, WhileBlockSyntaxNode stat)
        {
            _exitLabel = new LabelStatementSyntaxNode();
            _cond = new ConditionGenerator(factory, block, stat.Condition, _exitLabel, reverseCondition: false);
            _block = new BlockGenerator(factory, block.Stack, stat);
        }

        public override void Emit(InstructionWriter writer)
        {
            var restartLabel = new LabelStatementSyntaxNode();
            writer.MarkLabel(restartLabel);
            _cond.Emit(writer);
            
            _block.Emit(writer);

            writer.WriteUSx(OpCodes.JMP, 0, 0);
            writer.AddLabelFix(restartLabel, InstructionWriter.FixUSxJump);

            writer.MarkLabel(_exitLabel);
        }
    }
}
