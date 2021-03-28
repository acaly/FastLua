using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public sealed class FunctionExpressionSyntaxNode : ExpressionSyntaxNode
    {
        public NodeRef<ExternalFunctionReferenceSyntaxNode> Prototype { get; set; }
        public List<NodeRef<UpValueListSyntaxNode>> UpValueLists { get; } = new();

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<FunctionExpressionSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeR(bw, Prototype);
            SerializeRL(bw, UpValueLists);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            Prototype = DeserializeR<ExternalFunctionReferenceSyntaxNode>(br);
            DeserializeRL(br, UpValueLists);
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
            Prototype?.Resolve(dict);
            foreach (var u in UpValueLists)
            {
                u?.Resolve(dict);
            }
        }
    }
}
