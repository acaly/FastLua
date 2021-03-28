using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public sealed class TableConstructorFieldSyntaxNode : SyntaxNode
    {
        public ExpressionSyntaxNode Key { get; set; }
        public ExpressionSyntaxNode Value { get; set; }
        public bool IsLastField { get; set; }

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<TableConstructorFieldSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeO(bw, Key);
            SerializeO(bw, Value);
            SerializeV(bw, IsLastField);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            Key = DeserializeO<ExpressionSyntaxNode>(br);
            Value = DeserializeO<ExpressionSyntaxNode>(br);
            IsLastField = DeserializeV<bool>(br);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            Key?.Traverse(visitor);
            Value.Traverse(visitor);
            visitor.Finish(this);
        }
    }
}
