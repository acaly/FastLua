using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public sealed class IndexVariableSyntaxNode : VariableSyntaxNode
    {
        public ExpressionSyntaxNode Table { get; set; }
        public ExpressionSyntaxNode Key { get; set; }

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<IndexVariableSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeO(bw, Table);
            SerializeO(bw, Key);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            Table = DeserializeO<ExpressionSyntaxNode>(br);
            Key = DeserializeO<ExpressionSyntaxNode>(br);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            Table.Traverse(visitor);
            Key.Traverse(visitor);
            visitor.Finish(this);
        }
    }
}
