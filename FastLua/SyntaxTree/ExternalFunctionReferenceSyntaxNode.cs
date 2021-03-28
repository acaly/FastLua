using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public sealed class ExternalFunctionReferenceSyntaxNode : SyntaxNode
    {
        public int Index { get; set; } //0 is parent. Children from 1.
        public ulong GlobalFunctionId { get; set; }

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<ExternalFunctionReferenceSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeV(bw, Index);
            SerializeV(bw, GlobalFunctionId);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            Index = DeserializeV<int>(br);
            GlobalFunctionId = DeserializeV<ulong>(br);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            visitor.Finish(this);
        }
    }
}
