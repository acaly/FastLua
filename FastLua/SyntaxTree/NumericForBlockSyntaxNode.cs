using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public sealed class NumericForBlockSyntaxNode : LoopBlockSyntaxNode
    {
        public NodeRef<LocalVariableDefinitionSyntaxNode> Variable { get; set; }
        public ExpressionSyntaxNode From { get; set; }
        public ExpressionSyntaxNode To { get; set; }
        public ExpressionSyntaxNode Step { get; set; } //Can be null.

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<NumericForBlockSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeR(bw, Variable);
            SerializeO(bw, From);
            SerializeO(bw, To);
            SerializeO(bw, Step);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            Variable = DeserializeR<LocalVariableDefinitionSyntaxNode>(br);
            From = DeserializeO<ExpressionSyntaxNode>(br);
            To = DeserializeO<ExpressionSyntaxNode>(br);
            Step = DeserializeO<ExpressionSyntaxNode>(br);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            From.Traverse(visitor);
            To.Traverse(visitor);
            Step?.Traverse(visitor);
            visitor.Finish(this);
        }

        internal override void SetupReference(Dictionary<ulong, SyntaxNode> dict)
        {
            base.SetupReference(dict);
            Variable?.Resolve(dict);
        }
    }
}
