using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public enum LocalVariableKind
    {
        Unknown,
        UpValue,
        Argument,
        Local,
        Iterator,
    }

    public enum LocalVariableEscape
    {
        Unknown,
        ValueType,
        Escape,
        NoEscape,
        EscapeRet,
    }

    public sealed class LocalVariableDefinitionSyntaxNode : SyntaxNode
    {
        public LocalVariableKind Kind { get; set; }
        public NodeRef<BlockSyntaxNode> ParentBlock { get; set; } //Null for arguments and upvalues.
        public NodeRef<StatementSyntaxNode> Declaration { get; set; } //Null for arguments and upvalues.
        public SpecificationType Specification { get; set; }
        public LocalVariableEscape Escape { get; set; }
        public NodeRef<UpValueListSyntaxNode> UpValueList { get; set; } //The list this var belongs to (may be null).

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<LocalVariableDefinitionSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeV(bw, Kind);
            SerializeR(bw, ParentBlock);
            SerializeR(bw, Declaration);
            SerializeV(bw, Specification);
            SerializeV(bw, Escape);
            SerializeR(bw, UpValueList);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            Kind = DeserializeV<LocalVariableKind>(br);
            ParentBlock = DeserializeR<BlockSyntaxNode>(br);
            Declaration = DeserializeR<StatementSyntaxNode>(br);
            Specification = DeserializeV<SpecificationType>(br);
            Escape = DeserializeV<LocalVariableEscape>(br);
            UpValueList = DeserializeR<UpValueListSyntaxNode>(br);
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
            ParentBlock?.Resolve(dict);
            Declaration?.Resolve(dict);
            UpValueList?.Resolve(dict);
        }
    }
}
