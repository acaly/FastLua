using FastLua.SyntaxTree;
using FastLua.VM;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    internal class MultiReturnStatementGenerator : StatementGenerator
    {
        private readonly List<(ExpressionGenerator g, AllocatedLocal stack)> _fixedExpr = new();
        private readonly ExpressionGenerator _varargExpr;
        private readonly GroupStackFragment _varargStack;
        private readonly int _varargSig;

        private readonly int _retSig;
        private readonly SequentialStackFragment _sigFragment;

        public MultiReturnStatementGenerator(GeneratorFactory factory, BlockGenerator block, ReturnStatementSyntaxNode stat)
        {
            Debug.Assert(stat.Values.Expressions.Count != 0);

            var mergedSigFragment = new GroupStackFragment();
            factory.Function.SigBlockFragment.Add(mergedSigFragment);
            _sigFragment = new();
            mergedSigFragment.Add(_sigFragment);

            var sigWriter = new SignatureWriter();
            if (!stat.Values.HasVararg)
            {
                foreach (var e in stat.Values.Expressions)
                {
                    var g = factory.CreateExpression(block, e);
                    var stack = _sigFragment.AddSpecializedType(g.GetSingleType());
                    g.WritSig(sigWriter);
                    _fixedExpr.Add((g, stack));
                }
            }
            else
            {
                for (int i = 0; i < stat.Values.Expressions.Count - 1; ++i)
                {
                    var e = stat.Values.Expressions[i];
                    var g = factory.CreateExpression(block, e);
                    var stack = _sigFragment.AddSpecializedType(g.GetSingleType());
                    g.WritSig(sigWriter);
                    _fixedExpr.Add((g, stack));
                }
                //The last is similar but should be a SequentialStackFragment instead of a slot.
                {
                    //This is copied from InvocationExpressionGenerator. See comment there for
                    //the meaning of these two fragments.
                    var stackOverlapFragment = new OverlappedStackFragment();
                    mergedSigFragment.Add(stackOverlapFragment);

                    var stack = new GroupStackFragment();
                    stackOverlapFragment.Add(stack);

                    //Push a new sig fragment.
                    var lastOverlapFragment = factory.Function.SigBlockFragment;
                    factory.Function.SigBlockFragment = stackOverlapFragment;

                    //Create the vararg expression.
                    var e = stat.Values.Expressions[^1];
                    var g = factory.CreateExpression(block, e);

                    //Pop the sig fragment.
                    Debug.Assert(factory.Function.SigBlockFragment == stackOverlapFragment);
                    factory.Function.SigBlockFragment = lastOverlapFragment;

                    g.WritSig(sigWriter);
                    _varargExpr = g;
                    _varargStack = stack;

                    //We also need the sig for the last expr alone (to get its value with EmitGet).
                    var varargSigWriter = new SignatureWriter();
                    g.WritSig(varargSigWriter);
                    _varargSig = varargSigWriter.GetSignature(factory.Function.SignatureManager).i;
                }
            }
            _retSig = sigWriter.GetSignature(factory.Function.SignatureManager).i;
        }

        public override void Emit(InstructionWriter writer)
        {
            if (_retSig > 255 || _sigFragment.Offset > 255)
            {
                throw new NotImplementedException();
            }
            foreach (var (g, stack) in _fixedExpr)
            {
                g.EmitPrep(writer);
                g.EmitGet(writer, stack);
            }
            if (_varargExpr is not null)
            {
                //Note that the sig adjustment by RETN follows the rule of keeping vararg length
                //untouched (see Thread.SetSigBlock). This assumes the _varargExpr writes either
                //a fixed or a vararg slot in its WriteSig method. See comment in ExprList syntax.
                _varargExpr.EmitPrep(writer);
                _varargExpr.EmitGet(writer, _varargStack, _varargSig, keepSig: true);
            }
            writer.WriteUUU(Opcodes.RETN, _retSig, _sigFragment.Offset, 0);
        }
    }
}
