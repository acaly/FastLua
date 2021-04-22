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
    internal sealed class GenericForStatementGenerator : StatementGenerator
    {
        private readonly AssignmentStatementGenerator _assignment;
        private readonly BlockGenerator _forBlock;
        private readonly AllocatedLocal _hiddenVariableStack;
        private readonly AllocatedLocal _firstLoopVarStack;
        private readonly int _loopVarSig;
        private readonly StackSignature _loopVarSigType;

        public GenericForStatementGenerator(GeneratorFactory factory, BlockGenerator block, GenericForBlockSyntaxNode stat)
        {
            //Create a wrapper block.
            //Actually this is not necessary with the current design. We could add everything to parent block.
            //But let's still have a separation.
            //Note that this block does not contain any upvals and statements and won't be used in emit step.
            _forBlock = new BlockGenerator(factory, block.Stack, stat);

            var v1g = factory.Function.Locals[stat.HiddenVariableF];
            var v2g = factory.Function.Locals[stat.HiddenVariableS];
            var v3g = factory.Function.Locals[stat.HiddenVariableV];


            //To calculate the f,s,var tuple.
            //This will be inserted as the first statement of _forBlock.
            _assignment = new AssignmentStatementGenerator(factory, _forBlock, new() { v1g, v2g, v3g }, stat.ExpressionList);

            //Store the stack slot for f and var1.
            var ctrlVarOnStack = v1g.TryGetFromStack(out _hiddenVariableStack);
            Debug.Assert(ctrlVarOnStack);
            var var1OnStack = factory.Function.Locals[stat.LoopVariables[0]].TryGetFromStack(out _firstLoopVarStack);
            Debug.Assert(var1OnStack);

            //Get the return sig type.
            var sigWriter = new SignatureWriter();
            foreach (var loopVar in stat.LoopVariables)
            {
                factory.Function.Locals[loopVar].WritSig(sigWriter);
            }
            (_loopVarSigType, _loopVarSig) = sigWriter.GetSignature(factory.Function.SignatureManager);
        }

        public override void Emit(InstructionWriter writer)
        {
            Debug.Assert(_firstLoopVarStack.Offset == _hiddenVariableStack.Offset + 3);

            _forBlock.EmitUpvalLists(writer);

            var exitLabel = new LabelStatementSyntaxNode();
            var loopLabel = new LabelStatementSyntaxNode();

            //Insert assignment and FORG instruction (call iterator function and check for exit condition).
            _assignment.Emit(writer);

            writer.MarkLabel(loopLabel);
            if (_hiddenVariableStack.Offset > 255 || _loopVarSig > 255 || _loopVarSigType.FLength > 128)
            {
                throw new NotImplementedException();
            }

            writer.WriteUSx(OpCodes.FORG, _hiddenVariableStack.Offset, 0);
            writer.AddLabelFix(exitLabel, InstructionWriter.FixUSxJump);
            //Adjust right parameter: assume EmptyV.
            writer.WriteUUS(OpCodes.INV, _loopVarSig, (int)WellKnownStackSignature.EmptyV, -_loopVarSigType.FLength);

            //Emit inner block.
            _forBlock.EmitStatements(writer);

            //Jump back.
            writer.WriteUSx(OpCodes.JMP, 0, 0);
            writer.AddLabelFix(loopLabel, InstructionWriter.FixUSxJump);

            writer.MarkLabel(exitLabel);
        }
    }
}
