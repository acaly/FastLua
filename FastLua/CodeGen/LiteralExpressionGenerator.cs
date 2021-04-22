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

        public LiteralExpressionGenerator(FunctionGenerator func, LiteralExpressionSyntaxNode expr) : base(0)
        {
            var type = expr.SpecializationType.GetVMSpecializationType();
            var k = func.Constants;
            _constIndex = type switch
            {
                VMSpecializationType.Nil => k.GetUnspecializedNil(),
                VMSpecializationType.Bool => expr.BoolValue ? k.GetUnspecializedTrue() : k.GetUnspecializedFalse(),
                VMSpecializationType.Double => k.AddUnspecialized(TypedValue.MakeDouble(expr.DoubleValue)),
                VMSpecializationType.Int => k.AddUnspecialized(TypedValue.MakeInt(expr.Int32Value)),
                VMSpecializationType.String => k.AddUnspecialized(TypedValue.MakeString(expr.StringValue)),
                _ => throw new Exception(), //Internal exception.
            };

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
            writer.WriteUUU(OpCodes.K, dest.Offset, _constIndex, 0);
        }

        public override void EmitDiscard(InstructionWriter writer)
        {
        }
    }
}
