using FastLua.SyntaxTree;
using FastLua.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    internal class LocalVariableExpressionGenerator : ExpressionGenerator
    {
        private readonly VMSpecializationType _type;
        private readonly AllocatedLocal _localInfo;

        public LocalVariableExpressionGenerator(BlockStackFragment stack, LocalVariableDefinitionSyntaxNode definition)
        {
            _type = definition.Specialization.GetVMSpecializationType();
            _localInfo = stack.AddSpecializedType(_type);
        }

        public override bool TryGetSingleType(out VMSpecializationType type)
        {
            type = _type;
            return true;
        }

        public override bool TryGetFromStack(out AllocatedLocal stackOffset)
        {
            stackOffset = _localInfo;
            return true;
        }

        public override void EmitGet(InstructionWriter writer, AllocatedLocal dest)
        {
            var destIndex = dest.Offset;
            var offset = _localInfo.Offset;
            if (offset > 255 || destIndex > 255)
            {
                throw new NotImplementedException();
            }
            writer.WriteUUU(Opcodes.MOV, destIndex, offset, 0);
        }

        public override void EmitSet(InstructionWriter writer, AllocatedLocal src, VMSpecializationType type)
        {
            if (type != _type)
            {
                throw new NotImplementedException();
            }
            var srcIndex = src.Offset;
            var offset = _localInfo.Offset;
            if (offset > 255 || srcIndex > 255)
            {
                throw new NotImplementedException();
            }
            writer.WriteUUU(Opcodes.MOV, offset, srcIndex, 0);
        }
    }
}
