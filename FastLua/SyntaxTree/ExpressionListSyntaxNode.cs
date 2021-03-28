using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public sealed class ExpressionListSyntaxNode : SyntaxNode
    {
        public List<ExpressionSyntaxNode> Expressions { get; } = new();

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<ExpressionListSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeL(bw, Expressions);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            DeserializeL(br, Expressions);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            Expressions.Traverse(visitor);
            visitor.Finish(this);
        }
    }
}
