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
    internal class BinaryExpressionGenerator : ExpressionGenerator
    {
        private enum CalcMode
        {
            Normal, //Left, right, and result written to 3 different stack location.
            LeftToDest, //Left written to result's location.
            RightToDest, //Right written to result's location.
        }

        private readonly Opcodes _opcode;
        private readonly VMSpecializationType _type;
        private readonly CalcMode _mode;
        private readonly ExpressionGenerator _left, _right;
        private readonly AllocatedLocal? _leftTemp, _rightTemp;

        //This is used by the public ctor and ComparisonBinaryExpressionGenerator.
        protected BinaryExpressionGenerator(GeneratorFactory factory, BlockGenerator block,
            ExpressionGenerator left, ExpressionGenerator right, VMSpecializationType type)
            : base(factory)
        {
            _type = type;

            _left = left;
            _right = right;
            var leftType = left.GetSingleType();
            var rightType = right.GetSingleType();
            if (leftType != VMSpecializationType.Polymorphic ||
                rightType != VMSpecializationType.Polymorphic ||
                _type != VMSpecializationType.Polymorphic)
            {
                throw new NotImplementedException();
            }

            //Determine whether we can share the dest slot with one of the operands.
            //This saves a stack slot.
            //  ? = (expr) + a
            //will be generated as
            //  dest = (expr)
            //  dest = dest + a

            //Note that at this point we have no idea what variable (or temp variable)
            //will be assigned to this binary expr, so for the code
            //  a = (expr) + a
            //we will get an invalid code if we use the above pattern:
            //  a = (expr)
            //  a = a + a --> Wrong, the value that was in the second a has been lost!
            //This special case is handled during emit stage.
            var leftOnStack = left.TryGetFromStack(out _);
            var rightOnStack = right.TryGetFromStack(out _);
            if (leftType == _type && !leftOnStack)
            {
                _mode = CalcMode.LeftToDest;
                _rightTemp = block.TempAllocator.Allocate(rightType);
            }
            else if (rightType == _type && !rightOnStack)
            {
                _mode = CalcMode.RightToDest;
                _leftTemp = block.TempAllocator.Allocate(leftType);
            }
            else
            {
                _mode = CalcMode.Normal;
                if (!leftOnStack)
                {
                    _leftTemp = block.TempAllocator.Allocate(leftType);
                }
                if (!rightOnStack)
                {
                    _rightTemp = block.TempAllocator.Allocate(rightType);
                }
            }
        }

        //This public ctor works for arithmetic binary ops.
        public BinaryExpressionGenerator(GeneratorFactory factory, BlockGenerator block, BinaryExpressionSyntaxNode expr)
            : this(factory, block, factory.CreateExpression(block, expr.Left), factory.CreateExpression(block, expr.Right),
                  expr.SpecializationType.GetVMSpecializationType())
        {
            _opcode = expr.Operator.V switch
            {
                BinaryOperator.Raw.Add => Opcodes.ADD,
                BinaryOperator.Raw.Sub => Opcodes.SUB,
                BinaryOperator.Raw.Mul => Opcodes.MUL,
                BinaryOperator.Raw.Div => Opcodes.DIV,
                BinaryOperator.Raw.Mod => Opcodes.MOD,
                BinaryOperator.Raw.Pow => Opcodes.POW,
                _ => throw new Exception(), //Should not get here.
            };
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

        protected virtual void EmitInstruction(InstructionWriter writer, AllocatedLocal dest,
            AllocatedLocal leftStack, AllocatedLocal rightStack)
        {
            if (dest.Offset > 255 || leftStack.Offset > 255 || rightStack.Offset > 255)
            {
                throw new NotImplementedException();
            }
            writer.WriteUUU(_opcode, dest.Offset, leftStack.Offset, rightStack.Offset);
        }

        public override void EmitGet(InstructionWriter writer, AllocatedLocal dest)
        {
            //The two special cases.
            if (_mode == CalcMode.LeftToDest)
            {
                if (_right.TryGetFromStack(out var rightStack) && rightStack.Offset == dest.Offset)
                {
                    _left.EmitPrep(writer);
                    _left.EmitGet(writer, _rightTemp.Value);
                    EmitInstruction(writer, dest, _rightTemp.Value, rightStack);
                    return;
                }
            }
            else if (_mode == CalcMode.RightToDest)
            {
                if (_left.TryGetFromStack(out var leftStack) && leftStack.Offset == dest.Offset)
                {
                    _right.EmitPrep(writer);
                    _right.EmitGet(writer, _leftTemp.Value);
                    EmitInstruction(writer, dest, leftStack, _leftTemp.Value);
                    return;
                }
            }

            switch (_mode)
            {
            case CalcMode.LeftToDest:
            {
                _left.EmitPrep(writer);
                _left.EmitGet(writer, dest);
                if (!_right.TryGetFromStack(out var rightStack))
                {
                    rightStack = _rightTemp.Value;
                    _right.EmitPrep(writer);
                    _right.EmitGet(writer, rightStack);
                }
                EmitInstruction(writer, dest, dest, rightStack);
                break;
            }
            case CalcMode.RightToDest:
            {
                if (!_left.TryGetFromStack(out var leftStack))
                {
                    leftStack = _leftTemp.Value;
                    _left.EmitPrep(writer);
                    _left.EmitGet(writer, leftStack);
                }
                _right.EmitPrep(writer);
                _right.EmitGet(writer, dest);
                EmitInstruction(writer, dest, leftStack, dest);
                break;
            }
            case CalcMode.Normal:
            {
                if (!_left.TryGetFromStack(out var leftStack))
                {
                    leftStack = _leftTemp.Value;
                    _left.EmitPrep(writer);
                    _left.EmitGet(writer, leftStack);
                }
                if (!_right.TryGetFromStack(out var rightStack))
                {
                    rightStack = _rightTemp.Value;
                    _right.EmitPrep(writer);
                    _right.EmitGet(writer, rightStack);
                }
                EmitInstruction(writer, dest, leftStack, rightStack);
                break;
            }
            }
        }
    }
}
