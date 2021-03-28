using FastLua.SyntaxTree;
using System;
using System.IO;

namespace FastLuaExample
{
    class Program
    {
        static void Main(string[] args)
        {
            var tree = new SyntaxRoot()
            {
                RootFunction = new()
                {
                    Parameters = { },
                    ReturnNumber = FunctionReturnNumber.SingleRet,
                },
            };
            tree.RootFunction.MainBlock = new()
            {
                ParentFunction = new(tree.RootFunction),
            };
            tree.RootFunction.MainBlock.Statements.Add(new ReturnStatementSyntaxNode()
            {
                ParentBlock = new(tree.RootFunction.MainBlock),
                Values = new ExpressionListSyntaxNode()
                {
                    Expressions =
                    {
                        new LiteralExpressionSyntaxNode()
                        {
                            Int64Value = 123,
                        },
                    },
                },
            });

            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            tree.SerializeSyntaxTree(writer);

            stream.Seek(0, SeekOrigin.Begin);
            var reader = new BinaryReader(stream);
            var newTree = SyntaxRoot.DeserializeSyntaxTree(reader);
        }
    }
}
