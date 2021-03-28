using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public sealed class TableExpressionSyntaxNode : ExpressionSyntaxNode
    {
        public List<TableConstructorFieldSyntaxNode> Fields { get; } = new();

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<TableExpressionSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeL(bw, Fields);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            DeserializeL(br, Fields);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            Fields.Traverse(visitor);
            visitor.Finish(this);
        }
    }
}
