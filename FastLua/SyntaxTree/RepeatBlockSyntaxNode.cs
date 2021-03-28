using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public sealed class RepeatBlockSyntaxNode : LoopBlockSyntaxNode
    {
        public ExpressionSyntaxNode StopCondition { get; set; }

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<RepeatBlockSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeO(bw, StopCondition);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            StopCondition = DeserializeO<ExpressionSyntaxNode>(br);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            StopCondition.Traverse(visitor);
            visitor.Finish(this);
        }
    }
}
