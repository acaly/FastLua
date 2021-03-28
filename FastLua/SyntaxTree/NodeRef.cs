using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public class NodeRef<T> where T : SyntaxNode
    {
        public T Target { get; private set; }
        public ulong TargetId { get; private set; }

        public NodeRef(T target)
        {
            TargetId = target.NodeId;
            Target = target;
        }

        public NodeRef(ulong id)
        {
            TargetId = id;
        }

        internal void Resolve(Dictionary<ulong, SyntaxNode> node)
        {
            Target = (T)node[TargetId];
            TargetId = Target.NodeId;
        }
    }
}
