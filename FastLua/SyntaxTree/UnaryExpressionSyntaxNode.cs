using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public enum UnaryOperator
    {
        Unknown,
        Neg,
        Not,
        Num,
    }

    public sealed class UnaryExpressionSyntaxNode : ExpressionSyntaxNode
    {
        public const int Priority = 8;
        public UnaryOperator Operator { get; set; }
        public ExpressionSyntaxNode Operand { get; set; }

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<UnaryExpressionSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeV(bw, Operator);
            SerializeO(bw, Operand);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            Operator = DeserializeV<UnaryOperator>(br);
            Operand = DeserializeO<ExpressionSyntaxNode>(br);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            Operand.Traverse(visitor);
            visitor.Finish(this);
        }
    }
}
