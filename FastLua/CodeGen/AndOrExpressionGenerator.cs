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
    internal class AndOrExpressionGenerator : ExpressionGenerator
    {
        private readonly VMSpecializationType _type;
        private readonly Opcodes _opcode;
        private readonly ExpressionGenerator _left, _right;
        private readonly AllocatedLocal? _leftStack, _rightStack;

        public AndOrExpressionGenerator(GeneratorFactory factory, BlockGenerator block, BinaryExpressionSyntaxNode expr)
        {
            _type = expr.SpecializationType.GetVMSpecializationType();
            _left = factory.CreateExpression(block, expr.Left);
            _right = factory.CreateExpression(block, expr.Right);
            if (_left.GetSingleType() != _type)
            {
                _leftStack = block.TempAllocator.Allocate(_left.GetSingleType());
            }
            if (_right.GetSingleType() != _type)
            {
                _rightStack = block.TempAllocator.Allocate(_right.GetSingleType());
            }
            _opcode = expr.Operator.V == BinaryOperator.Raw.And ? Opcodes.ISFC : Opcodes.ISTC;
        }

        public override bool TryGetSingleType(out VMSpecializationType type)
        {
            type = _type;
            return true;
        }

        public override bool TryGetFromStack(out AllocatedLocal stackOffset)
        {
            stackOffset = default;
            return false;
        }

        public override void EmitGet(InstructionWriter writer, AllocatedLocal dest)
        {
            var leftStack = _leftStack ?? dest;
            var rightStack = _rightStack ?? dest;

            if (_left.GetSingleType() != _type || _right.GetSingleType() != _type)
            {
                //Needs conversion in MOV and ISTC.
                throw new NotImplementedException();
            }
            if (leftStack.Offset > 255 || rightStack.Offset > 255 || dest.Offset > 255)
            {
                throw new NotImplementedException();
            }

            //  emit leftStack = _left
            //  ISFC/ISTC dest, leftStack, exitLabel
            //  emit rightStack = _right
            //  MOV dest, rightStack
            //exitLabel:

            var exitLabel = new LabelStatementSyntaxNode();
            _left.EmitPrep(writer);
            _left.EmitGet(writer, leftStack);
            writer.WriteUUS(_opcode, dest.Offset, leftStack.Offset, 0);
            writer.AddLabelFix(exitLabel, InstructionWriter.FixUUSJump);
            _right.EmitPrep(writer);
            _right.EmitGet(writer, rightStack);
            //We already wrote to dest. No need to MOV.
            Debug.Assert(rightStack.Offset == dest.Offset);
            writer.MarkLabel(exitLabel);
        }
    }
}
