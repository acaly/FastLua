using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public sealed class ImportedUpValueListSyntaxNode : SyntaxNode
    {
        public int Index { get; set; }
        public int ExportId { get; set; }
        public UpValueListSyntaxNode UpValueList { get; set; }
        public List<NodeRef<LocalVariableDefinitionSyntaxNode>> Variables { get; } = new();

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<ImportedUpValueListSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeV(bw, Index);
            SerializeV(bw, ExportId);
            SerializeO(bw, UpValueList);
            SerializeRL(bw, Variables);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            Index = DeserializeV<int>(br);
            ExportId = DeserializeV<int>(br);
            UpValueList = DeserializeO<UpValueListSyntaxNode>(br);
            DeserializeRL(br, Variables);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            UpValueList.Traverse(visitor);
            visitor.Finish(this);
        }

        internal override void SetupReference(Dictionary<ulong, SyntaxNode> dict)
        {
            base.SetupReference(dict);
            foreach (var v in Variables)
            {
                v?.Resolve(dict);
            }
        }
    }
}
