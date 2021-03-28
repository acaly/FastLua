using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public sealed class IfSyntaxNode : StatementSyntaxNode
    {
        public List<ThenElseBlockSyntaxNode> Clauses { get; } = new();

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<IfSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeL(bw, Clauses);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            DeserializeL(br, Clauses);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            Clauses.Traverse(visitor);
            visitor.Finish(this);
        }
    }
}
