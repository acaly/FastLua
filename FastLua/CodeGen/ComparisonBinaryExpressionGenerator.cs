using FastLua.SyntaxTree;
using FastLua.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    class ComparisonBinaryExpressionGenerator : BinaryExpressionGenerator
    {
        private readonly bool _isReversed;
        private readonly Opcodes _opcode;
        private readonly int _true, _false;

        public ComparisonBinaryExpressionGenerator(GeneratorFactory factory, BlockGenerator block,
            BinaryExpressionSyntaxNode expr)
            : base(factory, block, CheckSwap(factory, block, expr, out var right, out var op, out var r), right,
                  expr.SpecializationType.GetVMSpecializationType())
        {
            _isReversed = r;
            _opcode = op;
            _true = factory.Function.Constants.GetUnspecializedTrue();
            _false = factory.Function.Constants.GetUnspecializedFalse();
            if (expr.SpecializationType.GetVMSpecializationType() != VMSpecializationType.Polymorphic)
            {
                //Code generated below only supports polymorphic.
                throw new NotImplementedException();
            }
        }

        private static ExpressionGenerator CheckSwap(GeneratorFactory factory, BlockGenerator block,
            BinaryExpressionSyntaxNode expr, out ExpressionGenerator r, out Opcodes op, out bool reversed)
        {
            var left = factory.CreateExpression(block, expr.Left);
            var right = factory.CreateExpression(block, expr.Right);
            op = expr.Operator.V switch
            {
                BinaryOperator.Raw.G => Opcodes.ISLT,
                BinaryOperator.Raw.GE => Opcodes.ISLE,
                BinaryOperator.Raw.L => Opcodes.ISLT,
                BinaryOperator.Raw.LE => Opcodes.ISLE,
                BinaryOperator.Raw.E => Opcodes.ISEQ,
                BinaryOperator.Raw.NE => Opcodes.ISNE,
                _ => throw new Exception(),
            };
            reversed = expr.Operator.V == BinaryOperator.Raw.G || expr.Operator.V == BinaryOperator.Raw.GE;
            r = right;
            return left;
        }

        protected override void EmitInstruction(InstructionWriter writer, AllocatedLocal dest,
            AllocatedLocal leftStack, AllocatedLocal rightStack)
        {
            if (dest.Offset > 255 || leftStack.Offset > 255 || rightStack.Offset > 255)
            {
                throw new NotImplementedException();
            }
            if (_true > 255 || _false > 255)
            {
                throw new NotImplementedException();
            }
            if (_isReversed)
            {
                //Swap left and right.
                var tmp = leftStack;
                leftStack = rightStack;
                rightStack = tmp;
            }

            //  ISXX left, right, trueLabel
            //  dest = false
            //  jmp exitLabel
            //trueLabel:
            //  dest = true
            //exitLabel:

            var trueLabel = new LabelStatementSyntaxNode();
            var exitLabel = new LabelStatementSyntaxNode();

            writer.WriteUUS(_opcode, leftStack.Offset, rightStack.Offset, 0);
            writer.AddLabelFix(trueLabel, InstructionWriter.FixUUSJump);
            writer.WriteUUU(Opcodes.K, dest.Offset, _false, 0);
            writer.WriteUSx(Opcodes.JMP, 0, 0);
            writer.AddLabelFix(exitLabel, InstructionWriter.FixUSxJump);
            writer.MarkLabel(trueLabel);
            writer.WriteUUU(Opcodes.K, dest.Offset, _true, 0);
            writer.MarkLabel(exitLabel);
        }
    }
}
