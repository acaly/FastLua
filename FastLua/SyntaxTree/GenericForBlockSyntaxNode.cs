using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public sealed class GenericForBlockSyntaxNode : LoopBlockSyntaxNode
    {
        public List<NodeRef<LocalVariableDefinitionSyntaxNode>> LoopVariables { get; } = new();
        public ExpressionListSyntaxNode ExpressionList { get; set; }

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<GenericForBlockSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeRL(bw, LoopVariables);
            SerializeO(bw, ExpressionList);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            DeserializeRL(br, LoopVariables);
            ExpressionList = DeserializeO<ExpressionListSyntaxNode>(br);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            ExpressionList.Traverse(visitor);
            visitor.Finish(this);
        }

        internal override void SetupReference(Dictionary<ulong, SyntaxNode> dict)
        {
            base.SetupReference(dict);
            foreach (var v in LoopVariables)
            {
                v.Resolve(dict);
            }
        }
    }
}
