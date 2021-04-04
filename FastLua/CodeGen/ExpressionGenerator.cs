using FastLua.SyntaxTree;
using FastLua.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    internal abstract class ExpressionGenerator
    {
        public abstract void WritSig(SignatureWriter writer);
        public abstract bool TryGetFromStack(out int stackOffset);
        public abstract void Emit(InstructionWriter writer, AllocatedLocal dest);
    }

    internal class LocalVariableExpressionGenerator : ExpressionGenerator
    {
        private readonly VMSpecializationType _type;
        private readonly AllocatedLocal _localInfo;

        public LocalVariableExpressionGenerator(BlockStackFragment stack, LocalVariableDefinitionSyntaxNode definition)
        {
            _type = definition.Specialization.GetVMSpecializationType();
            _localInfo = stack.AddSpecializedType(_type);
        }

        public override void WritSig(SignatureWriter writer)
        {
            writer.AppendFixed(_type);
        }

        public override bool TryGetFromStack(out int stackOffset)
        {
            stackOffset = _localInfo.Offset;
            return true;
        }

        public override void Emit(InstructionWriter writer, AllocatedLocal dest)
        {
            var destIndex = dest.Offset;
            var offset = _localInfo.Offset;
            if (offset > 255 || destIndex > 255)
            {
                throw new NotImplementedException();
            }
            writer.WriteUUU(Opcodes.MOV, destIndex, offset, 0);
        }
    }

    internal class UpvalueExpressionGenerator : ExpressionGenerator
    {
        private readonly AllocatedLocal _upvalList;
        private readonly int _index;
        private readonly VMSpecializationType _type;

        public UpvalueExpressionGenerator(AllocatedLocal upvalList, int index, LocalVariableDefinitionSyntaxNode definition)
        {
            _upvalList = upvalList;
            _index = index;
            _type = definition.Specialization.GetVMSpecializationType();
        }

        public override void WritSig(SignatureWriter writer)
        {
            writer.AppendFixed(_type);
        }

        public override bool TryGetFromStack(out int stackOffset)
        {
            stackOffset = default;
            return false;
        }

        public override void Emit(InstructionWriter writer, AllocatedLocal dest)
        {
            var destIndex = dest.Offset;
            var listOffset = _upvalList.Offset;
            if (destIndex > 255 | listOffset > 255 | _index > 255)
            {
                throw new NotImplementedException();
            }
            writer.WriteUUU(Opcodes.UGET, destIndex, listOffset, _index);
        }
    }
}
