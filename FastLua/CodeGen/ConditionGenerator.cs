using FastLua.SyntaxTree;
using FastLua.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    internal sealed class ConditionGenerator
    {
        private readonly bool _isComparison;
        private readonly Opcodes _opcode;
        private readonly ExpressionGenerator _expr1, _expr2;
        private readonly AllocatedLocal _expr1Stack, _expr2Stack;
        private readonly bool _exchangeComparison; //`expr2 cmp expr1` instead of `expr1 cmp expr2`
        private readonly LabelStatementSyntaxNode _falseLabel;

        public ConditionGenerator(GeneratorFactory factory, BlockGenerator block,
            ExpressionSyntaxNode expr, LabelStatementSyntaxNode falseLabel, bool reverseCondition)
        {
            while (expr is UnaryExpressionSyntaxNode unary && unary.Operator == UnaryOperator.Not)
            {
                expr = unary.Operand;
                reverseCondition = !reverseCondition;
            }

            if (expr is BinaryExpressionSyntaxNode bin && IsComparisonExpr(bin, out _opcode, out _exchangeComparison))
            {
                _isComparison = true;
                //Normally, reverse the cmp (jump to false label if condition is not met).
                if (!reverseCondition)
                {
                    ReverseComparisonResult(ref _opcode);
                }
                _expr1 = factory.CreateExpression(block, bin.Left);
                _expr2 = factory.CreateExpression(block, bin.Right);
                if (!_expr1.TryGetFromStack(out _))
                {
                    _expr1Stack = block.TempAllocator.Allocate(_expr1.GetSingleType());
                }
                if (!_expr2.TryGetFromStack(out _))
                {
                    _expr2Stack = block.TempAllocator.Allocate(_expr2.GetSingleType());
                }
            }
            else
            {
                //Normally use ISFC (jump to falseLabel if false).
                _opcode = reverseCondition ? Opcodes.ISTC : Opcodes.ISFC;

                _expr1 = factory.CreateExpression(block, expr);
                if (!_expr1.TryGetFromStack(out _))
                {
                    _expr1Stack = block.TempAllocator.Allocate(_expr1.GetSingleType());
                }
            }
            _falseLabel = falseLabel;

            //Jumping according to the expression's value should behave like a normal statement.
            //(Close any invocation expressions. Clear temp variable list.)
            factory.Function.CheckStatementState();
        }

        private static bool IsComparisonExpr(BinaryExpressionSyntaxNode expr, out Opcodes op, out bool exchanged)
        {
            switch (expr.Operator.V)
            {
            case BinaryOperator.Raw.L:
                op = Opcodes.ISLT;
                exchanged = false;
                return true;
            case BinaryOperator.Raw.LE:
                op = Opcodes.ISLE;
                exchanged = false;
                return true;
            case BinaryOperator.Raw.G:
                op = Opcodes.ISLT;
                exchanged = true;
                return true;
            case BinaryOperator.Raw.GE:
                op = Opcodes.ISLE;
                exchanged = true;
                return true;
            case BinaryOperator.Raw.E:
                op = Opcodes.ISEQ;
                exchanged = false;
                return true;
            case BinaryOperator.Raw.NE:
                op = Opcodes.ISNE;
                exchanged = false;
                return true;
            default:
                op = default;
                exchanged = default;
                return false;
            }
        }

        private static void ReverseComparisonResult(ref Opcodes op)
        {
            op = op switch
            {
                Opcodes.ISLT => Opcodes.ISNLT,
                Opcodes.ISLE => Opcodes.ISNLE,
                Opcodes.ISNLT => Opcodes.ISLT,
                Opcodes.ISNLE => Opcodes.ISLE,
                Opcodes.ISNE => Opcodes.ISEQ,
                _ => Opcodes.ISNE, //ISEQ
            };
        }

        public void Emit(InstructionWriter writer)
        {
            if (_isComparison)
            {
                if (!_expr1.TryGetFromStack(out var expr1Stack))
                {
                    expr1Stack = _expr1Stack;
                    _expr1.EmitPrep(writer);
                    _expr1.EmitGet(writer, expr1Stack);
                }
                if (!_expr2.TryGetFromStack(out var expr2Stack))
                {
                    expr2Stack = _expr2Stack;
                    _expr2.EmitPrep(writer);
                    _expr2.EmitGet(writer, expr2Stack);
                }
                if (expr1Stack.Offset > 255 || expr2Stack.Offset > 255)
                {
                    throw new NotImplementedException();
                }
                if (!_exchangeComparison)
                {
                    writer.WriteUUS(_opcode, expr1Stack.Offset, expr2Stack.Offset, 0);
                }
                else
                {
                    writer.WriteUUS(_opcode, expr2Stack.Offset, expr1Stack.Offset, 0);
                }
                writer.AddLabelFix(_falseLabel, InstructionWriter.FixUUSJump);
            }
            else
            {
                if (!_expr1.TryGetFromStack(out var expr1Stack))
                {
                    expr1Stack = _expr1Stack;
                    _expr1.EmitPrep(writer);
                    _expr1.EmitGet(writer, expr1Stack);
                }
                if (expr1Stack.Offset > 255)
                {
                    throw new NotImplementedException();
                }
                //ISTC/ISFC with A=B (copy to temp var itself).
                writer.WriteUUS(_opcode, expr1Stack.Offset, expr1Stack.Offset, 0);
                writer.AddLabelFix(_falseLabel, InstructionWriter.FixUUSJump);
            }
        }
    }
}
