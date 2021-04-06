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
    internal class UnaryExpressionGenerator : ExpressionGenerator
    {
        private readonly VMSpecializationType _type;
        private readonly ExpressionGenerator _operand;
        private readonly UnaryOperator _operator;
        private readonly AllocatedLocal? _tmpSlot;

        public UnaryExpressionGenerator(GeneratorFactory factory, BlockGenerator block, UnaryExpressionSyntaxNode expr)
            : base(factory)
        {
            _operand = factory.CreateExpression(block, expr.Operand);
            _operator = expr.Operator;
            _type = expr.SpecializationType.GetVMSpecializationType();
            if (!_operand.TryGetFromStack(out _))
            {
                _tmpSlot = block.TempAllocator.Allocate(_operand.GetSingleType());
            }
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
            int operand;
            if (_tmpSlot.HasValue)
            {
                _operand.EmitGet(writer, _tmpSlot.Value);
                operand = _tmpSlot.Value.Offset;
            }
            else
            {
                var onStack = _operand.TryGetFromStack(out var s);
                Debug.Assert(onStack);
                operand = s.Offset;
            }
            var destIndex = dest.Offset;
            if (operand > 255 ||destIndex > 255)
            {
                throw new NotImplementedException();
            }
            switch (_operator)
            {
            case UnaryOperator.Neg:
                writer.WriteUUU(Opcodes.NEG, destIndex, operand, 0);
                break;
            case UnaryOperator.Not:
                writer.WriteUUU(Opcodes.NOT, destIndex, operand, 0);
                break;
            case UnaryOperator.Num:
                writer.WriteUUU(Opcodes.LEN, destIndex, operand, 0);
                break;
            }
        }
    }
}
