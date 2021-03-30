using FastLua.SyntaxTree;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.Diagnostics
{
    //A reflection-based dumper for syntax tree.
    public static class SyntaxTreeDump
    {
        public static void Dump(this SyntaxNode node, TextWriter writer)
        {
            node.Traverse(new DumpVisitor { Output = writer });
        }

        public static string ToDumpName(this SyntaxNode node)
        {
            if (node is null)
            {
                return "<null>";
            }
            return $"#{node.NodeId}: {node.GetType().Name}";
        }

        private class DumpVisitor : ISyntaxTreeVisitor
        {
            public TextWriter Output { get; init; }
            private readonly HashSet<ulong> _visitedId = new();
            private SyntaxNode _lastVisited = null;
            private readonly List<SyntaxNode> _stack = new();

            public void Start(SyntaxNode node)
            {
                if (_lastVisited != node)
                {
                    throw new Exception($"Invalid visiter start at #{node.ToDumpName()}");
                }
                _stack.Add(node);
            }

            public void Finish(SyntaxNode node)
            {
                if (_stack.Count == 0 || _stack[^1] != node)
                {
                    throw new Exception($"Invalid visiter finish at #{node.ToDumpName()}");
                }
                _stack.RemoveAt(_stack.Count - 1);
            }

            public void Visit(SyntaxNode node)
            {
                if (node is null)
                {
                    Output.WriteLine($"{_visitedId.Count:0000}{new string(' ', 2 * _stack.Count)}<null>");
                }
                _lastVisited = node;
                if (!_visitedId.Add(node.NodeId))
                {
                    throw new Exception($"{node.ToDumpName()} is visited twice.");
                }
                Output.WriteLine($"{_visitedId.Count:0000}{new string(' ', 2 * _stack.Count)}{node.ToDumpName()}");
                foreach (var (k, v) in ListProperties(node))
                {
                    Output.WriteLine($"    {new string(' ', 2 * _stack.Count)}* {k} = {v}");
                }
            }

            private static IEnumerable<(string key, string value)> ListProperties(SyntaxNode node)
            {
                foreach (var p in node.GetType().GetProperties())
                {
                    if (p.Name == nameof(SyntaxNode.NodeId) || p.Name == nameof(SyntaxNode.DeserializeNodeId))
                    {
                        continue;
                    }
                    if (p.PropertyType.IsValueType)
                    {
                        string valStr;
                        try
                        {
                            valStr = p.GetValue(node).ToString();
                        }
                        catch
                        {
                            valStr = "<err>";
                        }
                        yield return (p.Name, valStr);
                    }
                    else if (p.PropertyType.IsGenericType &&
                        p.PropertyType.GetGenericTypeDefinition() == typeof(NodeRef<>))
                    {
                        var r = p.GetValue(node);
                        if (r is null)
                        {
                            yield return (p.Name, "<noderef: null>");
                        }
                        else
                        {
                            var target = (SyntaxNode)r.GetType().GetProperty(nameof(NodeRef<SyntaxNode>.Target)).GetValue(r);
                            if (target is null)
                            {
                                throw new Exception("Null node reference.");
                            }
                            yield return (p.Name, $"<noderef: {target.ToDumpName()}");
                        }
                    }
                    else if (typeof(SyntaxNode).IsAssignableFrom(p.PropertyType))
                    {
                        var r = (SyntaxNode)p.GetValue(node);
                        yield return (p.Name, r.ToDumpName());
                    }
                    else if (p.PropertyType.IsGenericType &&
                        p.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        var list = (IList)p.GetValue(node);
                        var elementType = p.PropertyType.GetGenericArguments()[0];
                        if (typeof(SyntaxNode).IsAssignableFrom(elementType))
                        {
                            yield return (p.Name, $"<node list: {list.Count}>");
                        }
                        else if (elementType.IsGenericType &&
                            elementType.GetGenericTypeDefinition() == typeof(NodeRef<>))
                        {
                            yield return (p.Name, $"<noderef list: {list.Count}>");
                        }
                        else
                        {
                            throw new Exception($"Invalid property on {node.GetType().Name}.");
                        }
                    }
                    else if (p.PropertyType == typeof(string))
                    {
                        string strVal;
                        try
                        {
                            strVal = (string)p.GetValue(node);
                        }
                        catch
                        {
                            strVal = "<err>";
                        }
                        yield return (p.Name, strVal);
                    }
                    else
                    {
                        throw new Exception($"Invalid property on {node.GetType().Name}.");
                    }
                }
            }
        }
    }
}
