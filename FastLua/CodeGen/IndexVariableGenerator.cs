using FastLua.SyntaxTree;
using FastLua.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    //Note that this is only for EmitSet (lhs of assignment). For EmitGet, use IndexExpressionGenerator
    internal class IndexVariableGenerator : ExpressionGenerator
    {
        private readonly VMSpecializationType _type;
        private readonly AllocatedLocal? _tableStack, _keyStack;
        private readonly ExpressionGenerator _table, _key;

        public IndexVariableGenerator(GeneratorFactory factory, BlockGenerator block, IndexVariableSyntaxNode expr)
            : base(factory)
        {
            _table = factory.CreateExpression(block, expr.Table);
            _key = factory.CreateExpression(block, expr.Key);
            _type = expr.SpecializationType.GetVMSpecializationType();
            if (!_table.TryGetFromStack(out _))
            {
                _tableStack = block.TempAllocator.Allocate(expr.Table);
            }
            if (!_key.TryGetFromStack(out _))
            {
                _keyStack = block.TempAllocator.Allocate(expr.Key);
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

        public override void EmitPrep(InstructionWriter writer)
        {
            if (_tableStack.HasValue)
            {
                _table.EmitGet(writer, _tableStack.Value);
            }
            if (_keyStack.HasValue)
            {
                _key.EmitGet(writer, _keyStack.Value);
            }
        }

        public override void EmitSet(InstructionWriter writer, AllocatedLocal src, VMSpecializationType type)
        {
            if (!_table.TryGetFromStack(out var tableStack))
            {
                tableStack = _tableStack.Value;
            }
            if (!_key.TryGetFromStack(out var keyStack))
            {
                keyStack = _keyStack.Value;
            }
            if (tableStack.Offset > 255 || keyStack.Offset > 255 || src.Offset > 255)
            {
                throw new NotImplementedException();
            }
            writer.WriteUUU(Opcodes.TSET, src.Offset, tableStack.Offset, keyStack.Offset);
        }

        public override void EmitGet(InstructionWriter writer, AllocatedLocal dest)
        {
            throw new NotSupportedException();
        }

        public override void EmitDiscard(InstructionWriter writer)
        {
            throw new NotSupportedException();
        }
    }
}
