using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public abstract class StatementSyntaxNode : SyntaxNode
    {
        public NodeRef<BlockSyntaxNode> ParentBlock { get; set; }

        internal override void Serialize(BinaryWriter bw)
        {
            base.Serialize(bw);
            SerializeR(bw, ParentBlock);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            ParentBlock = DeserializeR<BlockSyntaxNode>(br);
        }

        internal override void SetupReference(Dictionary<ulong, SyntaxNode> dict)
        {
            base.SetupReference(dict);
            ParentBlock?.Resolve(dict);
        }
    }
}
