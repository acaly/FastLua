using FastLua.SyntaxTree;
using FastLua.VM;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    using FunctionLocalDictionary = Dictionary<LocalVariableDefinitionSyntaxNode, ExpressionGenerator>;

    internal class FunctionGenerator
    {
        public readonly GroupStackFragment Stack;
        public readonly BlockStackFragment ArgumentFragment;
        public readonly BlockStackFragment UpvalFragment;
        public readonly GroupStackFragment LocalFragment;
        private readonly OverlappedStackFragment _rootSigBlockFragment;
        public OverlappedStackFragment SigBlockFragment;
        public readonly AllocatedLocal NullSlot; //Dest for discarded op results. Never read.

        public readonly SignatureManager SignatureManager;
        public readonly FunctionLocalDictionary Locals = new();
        public readonly List<(Proto, ImmutableArray<int>)> ChildFunctions = new();
        public readonly ConstantWriter Constants = new();
        public readonly Dictionary<UpValueListSyntaxNode, AllocatedLocal> UpvalueListSlots = new();

        public FunctionDefinitionSyntaxNode FunctionDefinition { get; private set; }
        public Func<ulong, Proto> ChildFunctionCompiler { get; private set; }

        public FunctionGenerator(SignatureManager signatureManager)
        {
            SignatureManager = signatureManager;

            Stack = new();

            ArgumentFragment = new();
            Stack.Add(ArgumentFragment);

            UpvalFragment = new();
            Stack.Add(UpvalFragment);

            var nullSlotFragment = new BlockStackFragment();
            Stack.Add(nullSlotFragment);
            NullSlot = nullSlotFragment.AddUnspecialized();

            LocalFragment = new();
            Stack.Add(LocalFragment);

            _rootSigBlockFragment = new();
            SigBlockFragment = _rootSigBlockFragment;
            Stack.Add(_rootSigBlockFragment);
        }

        public Proto Generate(FunctionDefinitionSyntaxNode funcNode, Func<ulong, Proto> childFunctionCompiler)
        {
            FunctionDefinition = funcNode;
            ChildFunctionCompiler = childFunctionCompiler;

            //Add parameters.
            SignatureWriter paramTypeList = new();
            SignatureWriter varargTypeList = new();
            foreach (var p in funcNode.Parameters)
            {
                var paramGenerator = new LocalVariableExpressionGenerator(ArgumentFragment, p);
                Locals.Add(p, paramGenerator);
                paramGenerator.WritSig(paramTypeList);
            }
            if (funcNode.HasVararg)
            {
                var varargType = funcNode.VarargType.GetVMSpecializationType();
                paramTypeList.AppendVararg(varargType);
                varargTypeList.AppendVararg(varargType);
            }

            //Add upvalues
            foreach (var upList in funcNode.ImportedUpValueLists)
            {
                var listLocal = UpvalFragment.AddObject();
                for (int i = 0; i < upList.Variables.Count; ++i)
                {
                    var upvalGenerator = new UpvalueExpressionGenerator(listLocal, i, upList.Variables[i]);
                    Locals.Add(upList.Variables[i], upvalGenerator);
                }

                //Unlike block-level upval lists, imported upval list may or may not be exported.
                //Need to check before adding.
                if (upList.UpValueList is not null)
                {
                    UpvalueListSlots.Add(upList.UpValueList, listLocal);
                }
            }

            //Create main block (this will recursively create all blocks and thus all locals).
            var factory = new GeneratorFactory(this);
            var block = new BlockGenerator(factory, LocalFragment, funcNode);
            block.CheckBlockStatementState();

            //Build locals.
            int stackLength = 0;
            Stack.Build(ref stackLength);

            //Generate code.
            var instructions = new InstructionWriter();
            block.Emit(instructions);

            //Ensure there is a ret as the last instruction.
            bool hasRet = false;
            if (instructions.Count > 0)
            {
                var (opcode, _, _, _) = instructions.ReadUUU(instructions.Count - 1);
                if (opcode == OpCodes.RET0 || opcode == OpCodes.RETN)
                {
                    hasRet = true;
                }
            }
            if (!hasRet)
            {
                instructions.WriteUUU(OpCodes.RET0, 0, 0, 0);
            }

            //Fix jumps.
            instructions.RunFix();

            return new Proto
            {
                ChildFunctions = ChildFunctions.ToImmutableArray(),
                ParameterSig = paramTypeList.GetSignature(SignatureManager).s,
                VarargSig = varargTypeList.GetSignature(SignatureManager).s,
                SigTypes = SignatureManager.ToArray(),
                Constants = Constants.ToImmutableArray(),
                Instructions = instructions.ToImmutableArray(),
                StackSize = stackLength,
                LocalRegionOffset = LocalFragment.Offset,
                SigRegionOffset = SigBlockFragment.Offset,
                UpvalRegionOffset = UpvalFragment.Offset,
            };
        }

        //Called after each statement (by BlockGenerator) to check the state of the generator.
        public void CheckFuncStatementState()
        {
            //Confirm that anyone who changes sig block (should only be InvocationExpr) changes it back.
            Debug.Assert(SigBlockFragment == _rootSigBlockFragment);
        }
    }
}
