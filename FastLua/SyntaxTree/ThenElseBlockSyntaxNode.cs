using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public sealed class ThenElseBlockSyntaxNode : BlockSyntaxNode
    {
        public ExpressionSyntaxNode Condition { get; set; }

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<ThenElseBlockSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeO(bw, Condition);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            Condition = DeserializeO<ExpressionSyntaxNode>(br);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            Condition.Traverse(visitor);
            visitor.Finish(this);
        }
    }
}
