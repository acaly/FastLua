using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    //This is the node at the export side.
    //Import side use ImportedUpValueListSyntaxNode.
    //Note that imported list can be directly exported, in which case there are both.
    public sealed class UpValueListSyntaxNode : SyntaxNode
    {
        public NodeRef<BlockSyntaxNode> ParentBlock { get; set; } //Null for imported lists.
        public List<NodeRef<LocalVariableDefinitionSyntaxNode>> Variables { get; } = new();
        public List<NodeRef<ExternalFunctionReferenceSyntaxNode>> ReferencedBy { get; } = new();

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<UpValueListSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeR(bw, ParentBlock);
            SerializeRL(bw, Variables);
            SerializeRL(bw, ReferencedBy);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
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
