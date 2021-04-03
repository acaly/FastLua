using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public enum FunctionReturnNumber
    {
        NoRet,
        SingleRet,
        MultiRet,
    }

    public sealed class FunctionDefinitionSyntaxNode : BlockSyntaxNode
    {
        private static ulong _nextGlobalId = 1;
        public static ulong CreateGlobalId() => Interlocked.Increment(ref _nextGlobalId);

        public ulong GlobalId { get; set; }
        public ExternalFunctionReferenceSyntaxNode ParentExternalFunction { get; set; }
        public List<ExternalFunctionReferenceSyntaxNode> ChildrenFunctions { get; } = new();

        //Nodes in this list provide the LocalVariableDefinitionSyntaxNode.
        //Index of this list is called ImportId, used to make the closure obj.
        public List<ImportedUpValueListSyntaxNode> ImportedUpValueLists { get; } = new();

        public List<LocalVariableDefinitionSyntaxNode> Parameters { get; } = new();

        public bool HasVararg { get; set; }
        public SpecializationType VarargType { get; set; }
        public FunctionReturnNumber ReturnNumber { get; set; }

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<FunctionDefinitionSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeV(bw, GlobalId);
            SerializeO(bw, ParentExternalFunction);
            SerializeL(bw, ChildrenFunctions);
            SerializeL(bw, ImportedUpValueLists);
            SerializeL(bw, Parameters);
            SerializeV(bw, HasVararg);
            SerializeV(bw, VarargType);
            SerializeV(bw, ReturnNumber);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            GlobalId = DeserializeV<ulong>(br);
            ParentExternalFunction = DeserializeO<ExternalFunctionReferenceSyntaxNode>(br);
            DeserializeL(br, ChildrenFunctions);
            DeserializeL(br, ImportedUpValueLists);
            DeserializeL(br, Parameters);
            HasVararg = DeserializeV<bool>(br);
            VarargType = DeserializeV<SpecializationType>(br);
            ReturnNumber = DeserializeV<FunctionReturnNumber>(br);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            ParentExternalFunction?.Traverse(visitor);
            ChildrenFunctions.Traverse(visitor);
            ImportedUpValueLists.Traverse(visitor);
            Parameters.Traverse(visitor);
            visitor.Finish(this);
        }
    }
}
