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
        public abstract bool TryGetFromStack(out int stackOffset);
        public abstract void Emit(List<uint> instructions, int dest);
    }

    internal class LocalVariableExpressionGenerator : ExpressionGenerator
    {
        private readonly AllocatedLocal _localInfo;

        public LocalVariableExpressionGenerator(AllocatedLocal localInfo)
        {
            _localInfo = localInfo;
        }

        public override bool TryGetFromStack(out int stackOffset)
        {
            stackOffset = _localInfo.Offset;
            return true;
        }

        public override void Emit(List<uint> instructions, int dest)
        {
            var offset = _localInfo.Offset;
            if (offset > 255 || dest > 255)
            {
                throw new NotImplementedException();
            }
            instructions.Add((uint)Opcodes.MOV << 24 | (uint)dest << 16 | (uint)offset << 8);
        }
    }

    internal class UpvalueExpressionGenerator : ExpressionGenerator
    {
        private readonly AllocatedLocal _upvalList;
        private readonly int _index;

        public UpvalueExpressionGenerator(AllocatedLocal upvalList, int index)
        {
            _upvalList = upvalList;
            _index = index;
        }

        public override bool TryGetFromStack(out int stackOffset)
        {
            stackOffset = default;
            return false;
        }

        public override void Emit(List<uint> instructions, int dest)
        {
            var listOffset = _upvalList.Offset;
            if (dest > 255 | listOffset > 255 | _index > 255)
            {
                throw new NotImplementedException();
            }
            instructions.Add((uint)Opcodes.UGET << 24 | (uint)dest << 16 | (uint)listOffset << 8 | (uint)_index);
        }
    }
}
