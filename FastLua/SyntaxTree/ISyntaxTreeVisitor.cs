using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public interface ISyntaxTreeVisitor
    {
        void Start(SyntaxNode node);
        void Finish(SyntaxNode node);
        void Visit(SyntaxNode node);
    }

    internal static class SyntaxTreeVisitorExtensions
    {
        public static void Traverse<T>(this List<T> nodes, ISyntaxTreeVisitor visitor)
            where T : SyntaxNode
        {
            foreach (var n in nodes)
            {
                n?.Traverse(visitor);
            }
        }
    }

    public sealed class SimpleSyntaxTreeVisitor : ISyntaxTreeVisitor
    {
        private readonly Action<SyntaxNode> _action;

        public SimpleSyntaxTreeVisitor(Action<SyntaxNode> action)
        {
            _action = action;
        }

        public void Start(SyntaxNode node)
        {
        }

        public void Finish(SyntaxNode node)
        {
        }

        public void Visit(SyntaxNode node)
        {
            _action(node);
        }
    }
}
