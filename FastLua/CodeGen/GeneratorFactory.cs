using FastLua.SyntaxTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    internal class GeneratorFactory
    {
        public readonly FunctionGenerator Function;

        public GeneratorFactory(FunctionGenerator function)
        {
            Function = function;
        }

        public ExpressionGenerator CreateExpression(BlockGenerator parentBlock, ExpressionSyntaxNode expr)
        {
            switch (expr)
            {
            case NamedVariableSyntaxNode nameVariable:
                return Function.Locals[nameVariable.Variable.Target];
            //TODO
            default:
                throw new Exception();
            }
        }

        public StatementGenerator CreateStatement(BlockGenerator parentBlock, StatementSyntaxNode statement)
        {
            switch (statement)
            {
            //TODO (other blocks must be before the BlockSyntaxNode)
            case BlockSyntaxNode block:
                return new BlockGenerator(this, parentBlock?.Stack ?? Function.LocalFragment, block);
            default:
                throw new Exception();
            }
        }
    }
}
