using FastLua.SyntaxTree;
using FastLua.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    internal class LiteralExpressionGenerator : ExpressionGenerator
    {
        private readonly VMSpecializationType _type;
        private readonly int _constIndex;

        public LiteralExpressionGenerator(FunctionGenerator func, LiteralExpressionSyntaxNode expr)
        {
            var type = expr.SpecializationType.GetVMSpecializationType();
            _constIndex = func.Constants.AddUnspecialized(type switch
            {
                VMSpecializationType.Nil => TypedValue.Nil,
                VMSpecializationType.Bool => expr.BoolValue ? TypedValue.True : TypedValue.False,
                VMSpecializationType.Double => TypedValue.MakeDouble(expr.DoubleValue),
                VMSpecializationType.Int => TypedValue.MakeInt(expr.Int32Value),
                VMSpecializationType.String => TypedValue.MakeString(expr.StringValue),
                _ => throw new Exception(), //Internal exception.
            });

            _type = type.Deoptimize();
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
            if (dest.Offset > 255 || _constIndex > 255)
            {
                throw new NotImplementedException();
            }
            writer.WriteUUU(Opcodes.K, dest.Offset, _constIndex, 0);
        }
    }
}
