using FastLua.SyntaxTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    internal sealed class RepeatStatementGenerator : StatementGenerator
    {
        private readonly BlockGenerator _block;
        private readonly ConditionGenerator _cond;
        private readonly LabelStatementSyntaxNode _repeatLabel;

        public RepeatStatementGenerator(GeneratorFactory factory, BlockGenerator block, RepeatBlockSyntaxNode stat)
        {
            _repeatLabel = new LabelStatementSyntaxNode();
            _block = new BlockGenerator(factory, block.Stack, stat);

            //Jump to _repeatLabel (falseLabel) if false.
            _cond = new ConditionGenerator(factory, _block, stat.StopCondition, _repeatLabel, reverseCondition: false);
        }

        public override void Emit(InstructionWriter writer)
        {
            writer.MarkLabel(_repeatLabel);
            _block.Emit(writer);
            _cond.Emit(writer);
        }
    }
}
