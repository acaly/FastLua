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
    using FunctionLocalDictionary = Dictionary<LocalVariableDefinitionSyntaxNode, ExpressionGenerator>;

    internal class FunctionGenerator
    {
        public readonly GroupStackFragment Stack;
        public readonly BlockStackFragment ArgumentFragment;
        public readonly BlockStackFragment UpvalFragment;
        public readonly GroupStackFragment LocalFragment;
        public readonly OverlappedStackFragment SigBlockFragment;

        public readonly SignatureManager SignatureManager;
        public readonly FunctionLocalDictionary FunctionLevelLocals = new();
        public readonly List<Proto> ChildFunctions = new();
        public readonly List<TypedValue> Constants = new();
        public readonly List<StackSignature> Signatures = new();

        public FunctionGenerator(SignatureManager signatureManager)
        {
            SignatureManager = signatureManager;

            Stack = new();
            ArgumentFragment = new();
            UpvalFragment = new();
            LocalFragment = new();
            SigBlockFragment = new();
            Stack.Add(ArgumentFragment);
            Stack.Add(UpvalFragment);
            Stack.Add(LocalFragment);
            Stack.Add(SigBlockFragment);
        }

        public Proto Generate(FunctionDefinitionSyntaxNode funcNode)
        {
            //Add parameters.
            List<VMSpecializationType> argTypeList = new();
            foreach (var p in funcNode.Parameters)
            {
                var vmType = p.Specialization.GetVMSpecializationType();
                FunctionLevelLocals.Add(p, new LocalVariableExpressionGenerator(ArgumentFragment.AddSpecializedType(vmType)));
                argTypeList.Add(vmType);
            }

            //Add upvalues
            foreach (var upList in funcNode.ImportedUpValueLists)
            {
                var listLocal = UpvalFragment.AddObj(1);
                for (int i = 0; i < upList.Variables.Count; ++i)
                {
                    FunctionLevelLocals.Add(upList.Variables[i],
                        new UpvalueExpressionGenerator(listLocal, i));
                }
            }

            //Create main block
            var block = new BlockGenerator(funcNode);

            //Build locals.
            int stackLength = 0;
            Stack.Build(ref stackLength);

            //Generate code.
            var instructions = new List<uint>();
            block.Generate(instructions);

            VMSpecializationType? varargType = funcNode.HasVararg ? funcNode.VarargType.GetVMSpecializationType() : null;
            var paramSig = SignatureManager.Get(argTypeList, varargType);
            return new Proto
            {
                ChildFunctions = ChildFunctions.ToImmutableArray(),
                ParameterSig = paramSig.GetDesc(),
                SigDesc = Signatures.Select(s => s.GetDesc()).ToArray(),
                Constants = Constants.ToImmutableArray(),
                Instructions = instructions.ToImmutableArray(),
                StackSize = stackLength,
                LocalRegionOffset = LocalFragment.Offset,
                SigRegionOffset = SigBlockFragment.Offset,
                UpvalRegionOffset = UpvalFragment.Offset,
            };
        }
    }
}
