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
        public int ImportId { get; set; } //Id in function's import list. Used to make the closure obj.
        public UpValueListSyntaxNode UpValueList { get; set; } //Only used when this list is also exported.
        public List<LocalVariableDefinitionSyntaxNode> Variables { get; } = new();

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<ImportedUpValueListSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeV(bw, ImportId);
            SerializeO(bw, UpValueList);
            SerializeL(bw, Variables);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            ImportId = DeserializeV<int>(br);
            UpValueList = DeserializeO<UpValueListSyntaxNode>(br);
            DeserializeL(br, Variables);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            UpValueList.Traverse(visitor);
            Variables.Traverse(visitor);
            visitor.Finish(this);
        }
    }
}
