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
    internal class VarargExpressionGenerator : ExpressionGenerator
    {
        private readonly bool _isVararg;
        private readonly VMSpecializationType _type;
        private readonly StackSignature _funcVarargSig;

        public VarargExpressionGenerator(GeneratorFactory factory, FunctionDefinitionSyntaxNode func, VarargExpressionSyntaxNode expr) : base(0)
        {
            _isVararg = expr.ReceiverMultiRetState == ExpressionReceiverMultiRetState.Variable;
            _type = func.VarargType.GetVMSpecializationType();
            _funcVarargSig = factory.Function.VarargSignature;
        }

        public override bool TryGetSingleType(out VMSpecializationType type)
        {
            if (_isVararg)
            {
                type = default;
                return false;
            }
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
            Debug.Assert(!_isVararg);
            if (dest.Offset > 255)
            {
                throw new NotImplementedException();
            }
            writer.WriteUUU(OpCodes.VARG1, dest.Offset, 0, 0);
        }

        public override void EmitGet(InstructionWriter writer, IStackFragment sigBlock, StackSignature sigType,
            int sigIndex, bool keepSig)
        {
            Debug.Assert(_isVararg);
            if (sigIndex > 255 || sigBlock.Offset > 255 | sigType.FixedSize > 127)
            {
                throw new NotImplementedException();
            }
            //Note that for VARG/VARGC, we are using a different version of R1-R3. See OpCodes doc for details.
            Debug.Assert(_funcVarargSig.FixedSize == 0);
            if (_funcVarargSig.IsCompatibleWith(sigType))
            {
                writer.WriteUUS(keepSig ? OpCodes.VARG : OpCodes.VARGC,
                    sigBlock.Offset, sigIndex, sigType.FixedSize);
            }
            else
            {
                writer.WriteUUS(keepSig ? OpCodes.VARG : OpCodes.VARGC,
                    sigBlock.Offset, sigIndex, -1);
            }
        }

        public override void WritSig(SignatureWriter writer)
        {
            if (_isVararg)
            {
                writer.AppendVararg(_type);
            }
            else
            {
                writer.AppendFixed(_type);
            }
        }

        public override void EmitDiscard(InstructionWriter writer)
        {
        }
    }
}
