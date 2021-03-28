using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public sealed class LabelStatementSyntaxNode : StatementSyntaxNode
    {
        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<LabelStatementSyntaxNode>(bw);
            base.Serialize(bw);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            visitor.Finish(this);
        }
    }
}
