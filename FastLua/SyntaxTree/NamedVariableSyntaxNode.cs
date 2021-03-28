using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public sealed class NamedVariableSyntaxNode : VariableSyntaxNode
    {
        public NodeRef<LocalVariableDefinitionSyntaxNode> Variable { get; set; }

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<NamedVariableSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeR(bw, Variable);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            Variable = DeserializeR<LocalVariableDefinitionSyntaxNode>(br);
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
            Variable?.Resolve(dict);
        }
    }
}
