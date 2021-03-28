using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public sealed class InvocationExpressionSyntaxNode : ExpressionSyntaxNode
    {
        public ExpressionSyntaxNode Function { get; set; }
        public ExpressionListSyntaxNode Args { get; set; }
        public bool HasSelf { get; set; }

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<InvocationExpressionSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeO(bw, Function);
            SerializeO(bw, Args);
            SerializeV(bw, HasSelf);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            Function = DeserializeO<ExpressionSyntaxNode>(br);
            Args = DeserializeO<ExpressionListSyntaxNode>(br);
            HasSelf = DeserializeV<bool>(br);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            Function.Traverse(visitor);
            Args.Traverse(visitor);
            visitor.Finish(this);
        }
    }
}
