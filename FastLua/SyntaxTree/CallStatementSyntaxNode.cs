using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public sealed class CallStatementSyntaxNode : StatementSyntaxNode
    {
        public InvocationExpressionSyntaxNode Invocation { get; set; }

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<CallStatementSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeO(bw, Invocation);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            Invocation = DeserializeO<InvocationExpressionSyntaxNode>(br);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            Invocation.Traverse(visitor);
            visitor.Finish(this);
        }
    }
}
