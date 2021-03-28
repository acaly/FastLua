using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public sealed class GotoStatementSyntaxNode : StatementSyntaxNode
    {
        public NodeRef<LabelStatementSyntaxNode> Target { get; set; }

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<GotoStatementSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeR(bw, Target);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            Target = DeserializeR<LabelStatementSyntaxNode>(br);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            visitor.Finish(this);
        }

        internal override void SetupReference(Dictionary<ulong, SyntaxNode> dict)
        {
            base.SetupReference(dict);
            Target?.Resolve(dict);
        }
    }
}
