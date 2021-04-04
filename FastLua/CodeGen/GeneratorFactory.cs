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
            case BinaryExpressionSyntaxNode binary:
            case FunctionExpressionSyntaxNode function:
            case IndexVariableSyntaxNode indexVariable:
            case InvocationExpressionSyntaxNode invocation:
                throw new NotImplementedException();
            case LiteralExpressionSyntaxNode literal:
                return new LiteralExpressionGenerator(Function, literal);
            case NamedVariableSyntaxNode nameVariable:
                return Function.Locals[nameVariable.Variable.Target];
            case TableExpressionSyntaxNode table:
            case UnaryExpressionSyntaxNode unary:
            case VarargExpressionSyntaxNode vararg:
                throw new NotImplementedException();
            default:
                throw new Exception();
            }
        }

        public StatementGenerator CreateStatement(BlockGenerator parentBlock, StatementSyntaxNode statement)
        {
            switch (statement)
            {
            case AssignmentStatementSyntaxNode assignment:
                return new AssignmentStatementGenerator(this, parentBlock,
                    assignment.Variables.Select(v => CreateExpression(parentBlock, v)).ToList(),
                    assignment.Values);
            case GenericForBlockSyntaxNode genericFor:
            case GotoStatementSyntaxNode @goto:
            case IfStatementSyntaxNode @if:
            case InvocationStatementSyntaxNode invocation:
            case LabelStatementSyntaxNode label:
                throw new NotImplementedException();
            case LocalStatementSyntaxNode local:
                return new AssignmentStatementGenerator(this, parentBlock,
                    local.Variables.Select(v => Function.Locals[v]).ToList(),
                    local.ExpressionList);
            case NumericForBlockSyntaxNode numericFor:
            case RepeatBlockSyntaxNode repeat:
            case ReturnStatementSyntaxNode @return:
            case WhileBlockSyntaxNode @while:
                throw new NotImplementedException();
            case BlockSyntaxNode block:
                //Block as the last (should only match simple block and function definition).
                return new BlockGenerator(this, parentBlock?.Stack ?? Function.LocalFragment, block);
            default:
                throw new Exception();
            }
        }
    }
}
