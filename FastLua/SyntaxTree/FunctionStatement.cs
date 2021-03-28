using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public sealed class FunctionStatement : StatementSyntaxNode
    {
        public VariableSyntaxNode FunctionName { get; set; }
        public bool HasThis { get; set; }
        public FunctionExpressionSyntaxNode Expression { get; set; }

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<FunctionStatement>(bw);
            base.Serialize(bw);
            SerializeO(bw, FunctionName);
            SerializeV(bw, HasThis);
            SerializeO(bw, Expression);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            FunctionName = DeserializeO<VariableSyntaxNode>(br);
            HasThis = DeserializeV<bool>(br);
            Expression = DeserializeO<FunctionExpressionSyntaxNode>(br);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            FunctionName.Traverse(visitor);
            Expression.Traverse(visitor);
            visitor.Finish(this);
        }
    }
}
