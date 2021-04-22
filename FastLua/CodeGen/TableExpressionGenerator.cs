using FastLua.SyntaxTree;
using FastLua.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    internal sealed class TableExpressionGenerator : ExpressionGenerator
    {
        private readonly VMSpecializationType _type;
        private readonly List<(ExpressionGenerator v, AllocatedLocal vs, ExpressionGenerator k, AllocatedLocal ks)> _fields = new();
        private readonly ExpressionGenerator _varargVal;
        private readonly BlockStackFragment _varargStack;
        private readonly OverlappedStackFragment _seqStack;
        private readonly int _sig, _varargSig;

        public TableExpressionGenerator(GeneratorFactory factory, BlockGenerator block, TableExpressionSyntaxNode expr)
            : base(0)
        {
            _type = expr.SpecializationType.GetVMSpecializationType();
            bool hasSeq = false;
            var mergedSig = new GroupStackFragment();
            factory.Function.SigBlockFragment.Add(mergedSig);
            var fixedSigFragment = new SequentialStackFragment();
            mergedSig.Add(fixedSigFragment);
            var varargSigFragment = new OverlappedStackFragment();
            mergedSig.Add(varargSigFragment);
            var sigWriter = new SignatureWriter();

            for (int i = 0; i < expr.Fields.Count; ++i)
            {
                var field = expr.Fields[i];
                var k = field.Key is not null ? factory.CreateExpression(block, field.Key) : null;
                var v = factory.CreateExpression(block, field.Value);
                if (k is null)
                {
                    hasSeq = true;
                    if (i == expr.Fields.Count - 1 &&
                        (field.Value.MultiRetState == ExpressionMultiRetState.MayBe ||
                        field.Value.MultiRetState == ExpressionMultiRetState.MustBe))
                    {
                        var varargSigWriter = new SignatureWriter();
                        _varargVal = v;
                        _varargStack = new();
                        varargSigFragment.Add(_varargStack);
                        v.WritSig(sigWriter);
                        v.WritSig(varargSigWriter);
                        _varargSig = varargSigWriter.GetSignature(factory.Function.SignatureManager).i;
                    }
                    else
                    {
                        var vt = v.GetSingleType();
                        var s = fixedSigFragment.AddSpecializedType(vt);
                        v.WritSig(sigWriter);
                        _fields.Add((v, s, null, default));
                    }
                }
                else
                {
                    var vt = v.GetSingleType();
                    AllocatedLocal vs = default, ks = default;
                    if (!k.TryGetFromStack(out _))
                    {
                        ks = block.TempAllocator.Allocate(k.GetSingleType());
                    }
                    if (!v.TryGetFromStack(out _))
                    {
                        vs = block.TempAllocator.Allocate(vt);
                    }
                    _fields.Add((v, vs, k, ks));
                }
                //TODO we should be able to clear temp allocator to reduce stack size here
            }

            if (hasSeq)
            {
                _sig = sigWriter.GetSignature(factory.Function.SignatureManager).i;
                _seqStack = factory.Function.SigBlockFragment;
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
            if (dest.Offset > 255)
            {
                throw new NotImplementedException();
            }
            writer.WriteUUU(OpCodes.TNEW, dest.Offset, 0, 0);
            bool hasSeq = false;
            foreach (var (v, vs_, k, ks_) in _fields)
            {
                if (k is null)
                {
                    v.EmitPrep(writer);
                    v.EmitGet(writer, vs_);
                    hasSeq = true;
                }
                else
                {
                    if (!k.TryGetFromStack(out var ks))
                    {
                        ks = ks_;
                    }
                    if (!v.TryGetFromStack(out var vs))
                    {
                        vs = vs_;
                    }
                    k.EmitPrep(writer);
                    k.EmitGet(writer, ks);
                    v.EmitPrep(writer);
                    v.EmitGet(writer, vs);
                    if (ks.Offset > 255 || vs.Offset > 255)
                    {
                        throw new NotImplementedException();
                    }
                    writer.WriteUUU(OpCodes.TSET, vs.Offset, dest.Offset, ks.Offset);
                }
            }
            if (_varargVal is not null)
            {
                _varargVal.EmitPrep(writer);
                _varargVal.EmitGet(writer, _varargStack, _varargSig, keepSig: true);
            }
            if (hasSeq)
            {
                if (_seqStack.Offset > 255 || _sig > 255)
                {
                    throw new NotImplementedException();
                }
                writer.WriteUUU(OpCodes.TINIT, dest.Offset, _seqStack.Offset, _sig);
            }
        }

        public override void EmitDiscard(InstructionWriter writer)
        {
            //Note that in this case the temp slots we allocated will not be used.
            foreach (var (val, _, key, _) in _fields)
            {
                key?.EmitDiscard(writer);
                val.EmitDiscard(writer);
            }
        }
    }
}
