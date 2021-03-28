using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public sealed class AssignmentStatementSyntaxNode : StatementSyntaxNode
    {
        public List<VariableSyntaxNode> Variables { get; } = new();
        public ExpressionListSyntaxNode Values { get; set; }

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<AssignmentStatementSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeL(bw, Variables);
            SerializeO(bw, Values);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            DeserializeL(br, Variables);
            Values = DeserializeO<ExpressionListSyntaxNode>(br);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            Variables.Traverse(visitor);
            Values.Traverse(visitor);
            visitor.Finish(this);
        }
    }
}
