using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    //TODO need a specification of retreturn
    public sealed class ReturnStatementSyntaxNode : StatementSyntaxNode
    {
        public ExpressionListSyntaxNode Values { get; set; }

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<ReturnStatementSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeO(bw, Values);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            Values = DeserializeO<ExpressionListSyntaxNode>(br);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            Values.Traverse(visitor);
            visitor.Finish(this);
        }
    }
}
