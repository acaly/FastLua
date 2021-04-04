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

        public bool HasVararg
        {
            get
            {
                if (Expressions.Count == 0) return false;
                var lastState = Expressions[^1].MultiRetState;
                return lastState == ExpressionMultiRetState.MayBe ||
                    lastState == ExpressionMultiRetState.MustBe;
            }
        }

        public int FixedCount => HasVararg ? Expressions.Count - 1 : Expressions.Count;

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
