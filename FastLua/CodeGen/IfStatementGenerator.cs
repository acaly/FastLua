using FastLua.SyntaxTree;
using FastLua.VM;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    using IfStatementClauseList = List<(ConditionGenerator, StatementGenerator, LabelStatementSyntaxNode)>;
    internal sealed class IfStatementGenerator : StatementGenerator
    {
        private readonly IfStatementClauseList _clauses = new();
        private readonly LabelStatementSyntaxNode _endIfLabel;

        public IfStatementGenerator(GeneratorFactory factory, BlockGenerator block, IfStatementSyntaxNode stat)
        {
            Debug.Assert(stat.Clauses.Count > 0);
            LabelStatementSyntaxNode lastLabel = null;
            foreach (var c in stat.Clauses)
            {
                var endClauseLabel = new LabelStatementSyntaxNode();
                var cond = c.Condition is null ? null : 
                    new ConditionGenerator(factory, block, c.Condition, endClauseLabel, reverseCondition: false);
                var clauseBlock = factory.CreateStatement(block, c); //As a normal block.
                _clauses.Add((cond, clauseBlock, endClauseLabel));
                lastLabel = endClauseLabel;
            }
            _endIfLabel = lastLabel;
        }
        
        public override void Emit(InstructionWriter writer)
        {
            for (int i = 0; i < _clauses.Count; ++i)
            {
                var (cond, block, label) = _clauses[i];
                if (cond is not null)
                {
                    cond.Emit(writer);
                }
                block.Emit(writer);

                //Skip rest clauses (unless it's already the last one).
                if (i != _clauses.Count - 1)
                {
                    writer.WriteUSx(Opcodes.JMP, 0, 0);
                    writer.AddLabelFix(_endIfLabel, InstructionWriter.FixUSxJump);
                }

                writer.MarkLabel(label);
            }
        }
    }
}
