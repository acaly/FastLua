using FastLua.SyntaxTree;
using FastLua.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    //Note that this class does not handle EmitSet (will throw). Use IndexVariableGenerator for that.
    internal class IndexExpressionGenerator : BinaryExpressionGenerator
    {
        public IndexExpressionGenerator(GeneratorFactory factory, BlockGenerator block, IndexVariableSyntaxNode expr)
            : base(factory, block, GetTable(factory, block, expr, out var key, out var type), key, type)
        {
        }

        private static ExpressionGenerator GetTable(GeneratorFactory factory, BlockGenerator block,
            IndexVariableSyntaxNode expr, out ExpressionGenerator key, out VMSpecializationType type)
        {
            var tab = factory.CreateExpression(block, expr.Table);
            key = factory.CreateExpression(block, expr.Key);
            type = expr.SpecializationType.GetVMSpecializationType();
            return tab;
        }

        protected override void EmitInstruction(InstructionWriter writer, AllocatedLocal dest,
            AllocatedLocal leftStack, AllocatedLocal rightStack)
        {
            if (dest.Offset > 255 || leftStack.Offset > 255 || rightStack.Offset > 255)
            {
                throw new NotImplementedException();
            }
            writer.WriteUUU(Opcodes.TGET, dest.Offset, leftStack.Offset, rightStack.Offset);
        }
    }
}
