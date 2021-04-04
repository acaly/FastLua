﻿using FastLua.SyntaxTree;
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
        public readonly FunctionLocalDictionary Locals = new();
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
            SignatureWriter paramTypeList = new();
            foreach (var p in funcNode.Parameters)
            {
                var paramGenerator = new LocalVariableExpressionGenerator(ArgumentFragment, p);
                Locals.Add(p, paramGenerator);
                paramGenerator.WritSig(paramTypeList);
            }
            if (funcNode.HasVararg)
            {
                paramTypeList.AppendVararg(funcNode.VarargType.GetVMSpecializationType());
            }

            //Add upvalues
            foreach (var upList in funcNode.ImportedUpValueLists)
            {
                var listLocal = UpvalFragment.AddObj(1);
                for (int i = 0; i < upList.Variables.Count; ++i)
                {
                    var upvalGenerator = new UpvalueExpressionGenerator(listLocal, i, upList.Variables[i]);
                    Locals.Add(upList.Variables[i], upvalGenerator);
                }
            }

            //Create main block (this will recursively create all blocks and thus all locals).
            var factory = new GeneratorFactory(this);
            var block = factory.CreateStatement(null, funcNode);

            //Build locals.
            int stackLength = 0;
            Stack.Build(ref stackLength);

            //Generate code.
            var instructions = new InstructionWriter();
            block.Emit(instructions);
            instructions.RunFix();

            return new Proto
            {
                ChildFunctions = ChildFunctions.ToImmutableArray(),
                ParameterSig = paramTypeList.GetSignature(SignatureManager).GetDesc(),
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
