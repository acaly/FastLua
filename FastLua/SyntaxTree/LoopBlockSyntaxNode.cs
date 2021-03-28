using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public abstract class LoopBlockSyntaxNode : BlockSyntaxNode
    {
        public NodeRef<LabelStatementSyntaxNode> BreakLabel { get; set; }

        internal override void Serialize(BinaryWriter bw)
        {
            base.Serialize(bw);
            SerializeR(bw, BreakLabel);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            BreakLabel = DeserializeR<LabelStatementSyntaxNode>(br);
        }

        internal override void SetupReference(Dictionary<ulong, SyntaxNode> dict)
        {
            base.SetupReference(dict);
            BreakLabel?.Resolve(dict);
        }
    }
}
