using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public abstract class BlockSyntaxNode : StatementSyntaxNode
    {
        public NodeRef<FunctionDefinitionSyntaxNode> ParentFunction { get; set; }
        public List<StatementSyntaxNode> Statements { get; } = new();
        public List<NodeRef<LocalVariableDefinitionSyntaxNode>> LocalVariables { get; } = new();
        public List<UpValueListSyntaxNode> UpValueLists { get; } = new();

        internal override void Serialize(BinaryWriter bw)
        {
            base.Serialize(bw);
            SerializeR(bw, ParentFunction);
            SerializeL(bw, Statements);
            SerializeRL(bw, LocalVariables);
            SerializeL(bw, UpValueLists);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            ParentFunction = DeserializeR<FunctionDefinitionSyntaxNode>(br);
            DeserializeL(br, Statements);
            DeserializeRL(br, LocalVariables);
            DeserializeL(br, UpValueLists);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            base.Traverse(visitor);
            Statements.Traverse(visitor);
            UpValueLists.Traverse(visitor);
        }

        internal override void SetupReference(Dictionary<ulong, SyntaxNode> dict)
        {
            base.SetupReference(dict);
            ParentFunction.Resolve(dict);
            foreach (var v in LocalVariables)
            {
                v?.Resolve(dict);
            }
        }
    }
}
