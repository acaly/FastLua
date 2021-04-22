using FastLua.SyntaxTree;
using FastLua.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    internal class ConcatBinaryExpressionGenerator : ExpressionGenerator
    {
        private readonly VMSpecializationType _type;
        private readonly List<ExpressionGenerator> _elements = new();
        private readonly List<AllocatedLocal> _temp = new();
        private readonly BlockStackFragment _tempFragment;

        public ConcatBinaryExpressionGenerator(GeneratorFactory factory, BlockGenerator block, BinaryExpressionSyntaxNode expr)
            : base(factory)
        {
            ExtractConcatList(factory, block, expr, _elements);
            _type = expr.SpecializationType.GetVMSpecializationType();
            if (_type != VMSpecializationType.Polymorphic ||
                _elements.Any(e => e.GetSingleType() != VMSpecializationType.Polymorphic))
            {
                //CONC instruction only works for polymorphic type.
                throw new NotImplementedException();
            }
            _tempFragment = new BlockStackFragment();
            factory.Function.SigBlockFragment.Add(_tempFragment);
            for (int i = 0; i < _elements.Count; ++i)
            {
                _temp.Add(_tempFragment.AddUnspecialized());
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
            for (int i = 0; i < _elements.Count; ++i)
            {
                _elements[i].EmitPrep(writer);
                _elements[i].EmitGet(writer, _temp[i]);
            }
            var begin = _tempFragment.Offset;
            var end = begin + _tempFragment.Length;
            if (dest.Offset > 255 || begin > 255 || end > 255)
            {
                throw new NotImplementedException();
            }
            writer.WriteUUU(OpCodes.CAT, dest.Offset, begin, end);
        }

        private static void ExtractConcatList(GeneratorFactory factory, BlockGenerator block,
            BinaryExpressionSyntaxNode expr, List<ExpressionGenerator> list)
        {
            var lhs = expr.Left;
            if (lhs is BinaryExpressionSyntaxNode lhsBinary && lhsBinary.Operator.V == BinaryOperator.Raw.Conc)
            {
                ExtractConcatList(factory, block, lhsBinary, list);
            }
            else
            {
                list.Add(factory.CreateExpression(block, lhs));
            }
            var rhs = expr.Right;
            if (rhs is BinaryExpressionSyntaxNode rhsBinary && rhsBinary.Operator.V == BinaryOperator.Raw.Conc)
            {
                ExtractConcatList(factory, block, rhsBinary, list);
            }
            else
            {
                list.Add(factory.CreateExpression(block, rhs));
            }
        }
    }
}
