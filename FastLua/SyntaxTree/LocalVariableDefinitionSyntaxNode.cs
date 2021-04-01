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
        Parameter,
        Local,
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
        public NodeRef<StatementSyntaxNode> Declaration { get; set; } //Only for local.
        public NodeRef<ImportedUpValueListSyntaxNode> ImportUpValueList { get; set; } //Only for upvalue.

        public SpecializationType Specialization { get; set; }
        public LocalVariableEscape Escape { get; set; }
        public NodeRef<UpValueListSyntaxNode> ExportUpValueList { get; set; } //The export list this var belongs to (may be null).

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<LocalVariableDefinitionSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeV(bw, Kind);
            SerializeR(bw, Declaration);
            SerializeR(bw, ImportUpValueList);
            SerializeV(bw, Specialization);
            SerializeV(bw, Escape);
            SerializeR(bw, ExportUpValueList);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            Kind = DeserializeV<LocalVariableKind>(br);
            Declaration = DeserializeR<StatementSyntaxNode>(br);
            ImportUpValueList = DeserializeR<ImportedUpValueListSyntaxNode>(br);
            Specialization = DeserializeV<SpecializationType>(br);
            Escape = DeserializeV<LocalVariableEscape>(br);
            ExportUpValueList = DeserializeR<UpValueListSyntaxNode>(br);
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
            Declaration?.Resolve(dict);
            ImportUpValueList?.Resolve(dict);
            ExportUpValueList?.Resolve(dict);
        }
    }
}
