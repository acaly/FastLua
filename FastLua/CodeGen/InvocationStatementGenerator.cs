using FastLua.SyntaxTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    internal class InvocationStatementGenerator : StatementGenerator
    {
        private readonly ExpressionGenerator _expr;

        public InvocationStatementGenerator(GeneratorFactory factory, BlockGenerator block, InvocationStatementSyntaxNode stat)
        {
            _expr = factory.CreateExpression(block, stat.Invocation);
        }

        public override void Emit(InstructionWriter writer)
        {
            _expr.EmitPrep(writer);
            _expr.EmitDiscard(writer);
        }
    }
}
