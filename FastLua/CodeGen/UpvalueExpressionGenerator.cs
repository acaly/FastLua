using FastLua.SyntaxTree;
using FastLua.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    internal class UpvalueExpressionGenerator : ExpressionGenerator
    {
        private readonly AllocatedLocal _upvalList;
        private readonly int _index;
        private readonly VMSpecializationType _type;

        public UpvalueExpressionGenerator(AllocatedLocal upvalList, int index, LocalVariableDefinitionSyntaxNode definition)
            : base(0)
        {
            _upvalList = upvalList;
            _index = index;
            _type = definition.Specialization.GetVMSpecializationType();
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
            var destIndex = dest.Offset;
            var listOffset = _upvalList.Offset;
            if (destIndex > 255 | listOffset > 255 | _index > 255)
            {
                throw new NotImplementedException();
            }
            writer.WriteUUU(OpCodes.UGET, destIndex, listOffset, _index);
        }

        public override void EmitSet(InstructionWriter writer, AllocatedLocal src, VMSpecializationType type)
        {
            if (type != _type)
            {
                throw new NotImplementedException();
            }
            var srcIndex = src.Offset;
            var listOffset = _upvalList.Offset;
            if (srcIndex > 255 | listOffset > 255 | _index > 255)
            {
                throw new NotImplementedException();
            }
            writer.WriteUUU(OpCodes.USET, srcIndex, listOffset, _index);
        }

        public override void EmitDiscard(InstructionWriter writer)
        {
        }
    }
}
