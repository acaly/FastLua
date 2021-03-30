using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using FastLua.Diagnostics;
using FastLua.Parser;
using FastLua.SyntaxTree;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace FastLuaExample
{
    public class Program
    {
        private static readonly string _code = "" +
            string.Concat(Enumerable.Repeat(
@"do
    local x = ""abc"";
    local y = function(i) return x .. 'x' end
    local count = 0
    local function onClick()
        count = count + 1
    end
end
", 1));
        private static readonly string _code2 = @"local count = 0 local function onclick() count = count + 1 end";

        public static void Main()
        {
            var codeReader = new StringReader(_code);
            var rawTokens = new LuaRawTokenizer();
            var luaTokens = new LuaTokenizer();
            rawTokens.Reset(codeReader);
            luaTokens.Reset(rawTokens);

            var parser = new LuaParser();
            var builder = new SyntaxTreeBuilder();
            builder.Start();
            luaTokens.EnsureMoveNext();
            parser.Reset(luaTokens, builder);
            parser.Parse();

            var ast = builder.Finish();
            ast.Dump(Console.Out);

            Console.WriteLine();
            Console.WriteLine("===============");
            Console.WriteLine();

            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            ast.Write(writer);

            stream.Seek(0, SeekOrigin.Begin);
            var reader = new BinaryReader(stream);
            var newTree = SyntaxRoot.Read(reader);
            newTree.Dump(Console.Out);
        }

        public static void Tokenizer()
        {
            var reader = new StringReader(_code);
            var rawTokens = new LuaRawTokenizer();
            var luaTokens = new LuaTokenizer();

            var timer = Stopwatch.StartNew();
            rawTokens.Reset(reader);
            luaTokens.Reset(rawTokens);
            foreach (var (type, content) in luaTokens)
            {
                if (type == LuaTokenType.EOS)
                {
                    Console.WriteLine($"EOS");
                }
                else if (type < LuaTokenType.First)
                {
                    Console.WriteLine($"{(char)type}");
                }
                else
                {
                    Console.WriteLine($"{type}\t{content.ToString()}");
                }
            }
        }

        public static void SerializeAST()
        {
            var tree = new SyntaxRoot()
            {
                RootFunction = new()
                {
                    Parameters = { },
                    ReturnNumber = FunctionReturnNumber.SingleRet,
                },
            };
            tree.RootFunction.Statements.Add(new ReturnStatementSyntaxNode()
            {
                ParentBlock = new(tree.RootFunction),
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
            tree.Write(writer);

            stream.Seek(0, SeekOrigin.Begin);
            var reader = new BinaryReader(stream);
            var newTree = SyntaxRoot.Read(reader);
        }
    }
}
