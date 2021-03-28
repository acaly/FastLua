using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public enum FunctionReturnNumber
    {
        NoRet,
        SingleRet,
        MultiRet,
    }

    public sealed class FunctionDefinitionSyntaxNode : SyntaxNode
    {
        public ExternalFunctionReferenceSyntaxNode ParentFunction { get; set; }
        public List<ExternalFunctionReferenceSyntaxNode> ChildrenFunctions { get; } = new();
        public List<ImportedUpValueListSyntaxNode> ImportedUpValueLists { get; } = new();
        public List<NodeRef<UpValueListSyntaxNode>> ExportedUpValueLists { get; } = new();

        public List<LocalVariableDefinitionSyntaxNode> Parameters { get; } = new();
        public SimpleBlockSyntaxNode MainBlock { get; set; }
        public bool HasVararg { get; set; }
        public FunctionReturnNumber ReturnNumber { get; set; }

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<FunctionDefinitionSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeO(bw, ParentFunction);
            SerializeL(bw, ChildrenFunctions);
            SerializeL(bw, ImportedUpValueLists);
            SerializeRL(bw, ExportedUpValueLists);
            SerializeL(bw, Parameters);
            SerializeO(bw, MainBlock);
            SerializeV(bw, HasVararg);
            SerializeV(bw, ReturnNumber);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            ParentFunction = DeserializeO<ExternalFunctionReferenceSyntaxNode>(br);
            DeserializeL(br, ChildrenFunctions);
            DeserializeL(br, ImportedUpValueLists);
            DeserializeRL(br, ExportedUpValueLists);
            DeserializeL(br, Parameters);
            MainBlock = DeserializeO<SimpleBlockSyntaxNode>(br);
            HasVararg = DeserializeV<bool>(br);
            ReturnNumber = DeserializeV<FunctionReturnNumber>(br);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            ParentFunction?.Traverse(visitor);
            ChildrenFunctions.Traverse(visitor);
            Parameters.Traverse(visitor);
            MainBlock.Traverse(visitor);
            visitor.Finish(this);
        }
    }
}
