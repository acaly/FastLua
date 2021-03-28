using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public sealed class UpValueListSyntaxNode : SyntaxNode
    {
        public int Id { get; set; } //Id in function, used to export to children functions.
        public NodeRef<BlockSyntaxNode> ParentBlock { get; set; } //Null for imported lists.
        public List<NodeRef<LocalVariableDefinitionSyntaxNode>> Variables { get; } = new();
        public List<NodeRef<ExternalFunctionReferenceSyntaxNode>> ReferencedBy { get; } = new();

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<UpValueListSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeV(bw, Id);
            SerializeR(bw, ParentBlock);
            SerializeRL(bw, Variables);
            SerializeRL(bw, ReferencedBy);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            Id = DeserializeV<int>(br);
            ParentBlock = DeserializeR<BlockSyntaxNode>(br);
            DeserializeRL(br, Variables);
            DeserializeRL(br, ReferencedBy);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            visitor.Finish(this);
        }

        internal override void SetupReference(Dictionary<ulong, SyntaxNode> dict)
        {
            base.SetupReference(dict);
            ParentBlock?.Resolve(dict);
            foreach (var v in Variables)
            {
                v?.Resolve(dict);
            }
            foreach (var r in ReferencedBy)
            {
                r?.Resolve(dict);
            }
        }
    }
}
