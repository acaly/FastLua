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
        public LocalVariableDefinitionSyntaxNode Variable { get; set; }
        public ExpressionSyntaxNode From { get; set; }
        public ExpressionSyntaxNode To { get; set; }
        public ExpressionSyntaxNode Step { get; set; } //Can be null.

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<NumericForBlockSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeO(bw, Variable);
            SerializeO(bw, From);
            SerializeO(bw, To);
            SerializeO(bw, Step);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            Variable = DeserializeO<LocalVariableDefinitionSyntaxNode>(br);
            From = DeserializeO<ExpressionSyntaxNode>(br);
            To = DeserializeO<ExpressionSyntaxNode>(br);
            Step = DeserializeO<ExpressionSyntaxNode>(br);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            Variable.Traverse(visitor);
            From.Traverse(visitor);
            To.Traverse(visitor);
            Step?.Traverse(visitor);
            visitor.Finish(this);
        }
    }
}
