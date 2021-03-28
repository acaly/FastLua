using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public sealed class LocalStatementSyntaxNode : StatementSyntaxNode
    {
        public List<LocalVariableDefinitionSyntaxNode> Variables { get; } = new();
        public ExpressionListSyntaxNode ExpressionList { get; set; }

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<LocalStatementSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeL(bw, Variables);
            SerializeO(bw, ExpressionList);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            DeserializeL(br, Variables);
            ExpressionList = DeserializeO<ExpressionListSyntaxNode>(br);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            Variables.Traverse(visitor);
            ExpressionList.Traverse(visitor);
            visitor.Finish(this);
        }
    }
}
