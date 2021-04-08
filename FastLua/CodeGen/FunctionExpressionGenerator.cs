using FastLua.SyntaxTree;
using FastLua.VM;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    internal sealed class FunctionExpressionGenerator : ExpressionGenerator
    {
        private readonly VMSpecializationType _type;
        private readonly Proto _proto;
        private readonly List<AllocatedLocal> _upvalueLists = new();

        private readonly FunctionGenerator _func;

        public FunctionExpressionGenerator(GeneratorFactory factory, FunctionExpressionSyntaxNode expr)
            : base(0)
        {
            _func = factory.Function;
            _type = expr.SpecializationType.GetVMSpecializationType();
            _proto = _func.ChildFunctionCompiler(expr.Prototype.Target.GlobalFunctionId);
            foreach (var upvalueList in expr.UpValueLists)
            {
                _upvalueLists.Add(_func.UpvalueListSlots[upvalueList.Target]);
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
            var id = _func.ChildFunctions.Count;
            _func.ChildFunctions.Add((_proto, _upvalueLists.Select(l => l.Offset).ToImmutableArray()));
            if (dest.Offset > 255 || id > 255)
            {
                throw new NotImplementedException();
            }
            writer.WriteUUU(Opcodes.FNEW, dest.Offset, id, 0);
        }

        public override void EmitDiscard(InstructionWriter writer)
        {
        }
    }
}
