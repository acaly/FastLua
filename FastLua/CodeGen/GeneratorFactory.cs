using FastLua.SyntaxTree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public ExpressionGenerator CreateVariable(BlockGenerator parentBlock, VariableSyntaxNode expr)
        {
            switch (expr)
            {
            case NamedVariableSyntaxNode namedVariable:
                //Same as expr (LocalVariableExprGen and UpvalueExprGen handles both get and set).
                return CreateExpression(parentBlock, namedVariable);
            case IndexVariableSyntaxNode indexVariable:
                return new IndexVariableGenerator(this, parentBlock, indexVariable);
            default:
                break;
            }
            throw new Exception();
        }

        public ExpressionGenerator CreateExpression(BlockGenerator parentBlock, ExpressionSyntaxNode expr)
        {
            switch (expr)
            {
            case BinaryExpressionSyntaxNode binary:
            {
                switch (binary.Operator.V)
                {
                case BinaryOperator.Raw.Add:
                case BinaryOperator.Raw.Sub:
                case BinaryOperator.Raw.Mul:
                case BinaryOperator.Raw.Div:
                case BinaryOperator.Raw.Pow:
                case BinaryOperator.Raw.Mod:
                    return new BinaryExpressionGenerator(this, parentBlock, binary);
                case BinaryOperator.Raw.Conc:
                    return new ConcatBinaryExpressionGenerator(this, parentBlock, binary);
                case BinaryOperator.Raw.L:
                case BinaryOperator.Raw.LE:
                case BinaryOperator.Raw.G:
                case BinaryOperator.Raw.GE:
                case BinaryOperator.Raw.E:
                case BinaryOperator.Raw.NE:
                    return new ComparisonBinaryExpressionGenerator(this, parentBlock, binary);
                case BinaryOperator.Raw.And:
                case BinaryOperator.Raw.Or:
                    return new AndOrExpressionGenerator(this, parentBlock, binary);
                default:
                    break;
                }
                break;
            }
            case FunctionExpressionSyntaxNode function:
                return new FunctionExpressionGenerator(this, function);
            case IndexVariableSyntaxNode indexVariable:
                return new IndexExpressionGenerator(this, parentBlock, indexVariable);
            case InvocationExpressionSyntaxNode invocation:
                return new InvocationExpressionGenerator(this, parentBlock, invocation);
            case LiteralExpressionSyntaxNode literal:
                return new LiteralExpressionGenerator(Function, literal);
            case NamedVariableSyntaxNode nameVariable:
            {
                //We don't create from the expr syntax node, so confirm the type is correct.
                var ret = Function.Locals[nameVariable.Variable.Target];
                Debug.Assert(nameVariable.SpecializationType.GetVMSpecializationType() == ret.GetSingleType());
                return ret;
            }
            case TableExpressionSyntaxNode table:
                return new TableExpressionGenerator(this, parentBlock, table);
            case UnaryExpressionSyntaxNode unary:
                return new UnaryExpressionGenerator(this, parentBlock, unary);
            case VarargExpressionSyntaxNode vararg:
                return new VarargExpressionGenerator(this, Function.FunctionDefinition, vararg);
            default:
                break;
            }
            throw new Exception();
        }

        public StatementGenerator CreateStatement(BlockGenerator parentBlock, StatementSyntaxNode statement)
        {
            switch (statement)
            {
            case AssignmentStatementSyntaxNode assignment:
                return new AssignmentStatementGenerator(this, parentBlock,
                    assignment.Variables.Select(v => CreateVariable(parentBlock, v)).ToList(),
                    assignment.Values);
            case GenericForBlockSyntaxNode genericFor:
                return new GenericForStatementGenerator(this, parentBlock, genericFor);
            case GotoStatementSyntaxNode @goto:
                return new GotoStatementGenerator(@goto.Target.Target);
            case IfStatementSyntaxNode @if:
                return new IfStatementGenerator(this, parentBlock, @if);
            case InvocationStatementSyntaxNode invocation:
                return new InvocationStatementGenerator(this, parentBlock, invocation);
            case LabelStatementSyntaxNode label:
                return new LabelStatementGenerator(label);
            case LocalStatementSyntaxNode local:
                return new AssignmentStatementGenerator(this, parentBlock,
                    local.Variables.Select(v => Function.Locals[v]).ToList(),
                    local.ExpressionList);
            case NumericForBlockSyntaxNode numericFor:
                return new NumericForStatementGenerator(this, parentBlock, numericFor);
            case RepeatBlockSyntaxNode repeat:
                return new RepeatStatementGenerator(this, parentBlock, repeat);
            case ReturnStatementSyntaxNode @return:
                if (@return.Values.Expressions.Count == 0)
                {
                    return new ZeroReturnStatementGenerator();
                }
                else
                {
                    return new MultiReturnStatementGenerator(this, parentBlock, @return);
                }
            case WhileBlockSyntaxNode @while:
                return new WhileStatementGenerator(this, parentBlock, @while);
            case BlockSyntaxNode block:
                //Block as the last (should only match simple block and function definition).
                return new BlockGenerator(this, parentBlock.Stack, block);
            default:
                break;
            }
            throw new Exception();
        }
    }
}
