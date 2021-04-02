using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using FastLua.Diagnostics;
using FastLua.Parser;
using FastLua.SyntaxTree;
using FastLua.VM;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Emit;

namespace FastLuaExample
{
    public class Program
    {
        private static readonly string _code = "" +
            string.Concat(Enumerable.Repeat(
@"do
    print(12 + 34)
    local x = ""abc"";
    local y = function(i) return x .. 'x' end
    local count = 0
    local function onClick()
        count = count + 1
    end
end
", 1));

        private static readonly string _code2 = @"local count = 0 local function onclick() count = count + 1 end";

        private static readonly string _code3 = @"
return function()
    local self = {}
    local x =
        function(__builder0)
            __builder0.OpenElement(0, ""p"")
            __builder0.AddMarkupContent(1, ""Hello"")
            __builder0.AddMarkupContent(2, "","")
            __builder0.AddMarkupContent(3, "" "")
            __builder0.AddMarkupContent(4, ""Blazor"")
            __builder0.AddMarkupContent(5, ""!"")
            __builder0.CloseElement()
        end
    local y = function(i)
        return
            function(__builder0)
                __builder0.OpenElement(6, ""p"")
                __builder0.AddMarkupContent(7, ""You"")
                __builder0.AddMarkupContent(8, "" "")
                __builder0.AddMarkupContent(9, ""clicked"")
                __builder0.AddMarkupContent(10, "" "")
                __builder0.AddContent(11, (i
                ))
                __builder0.AddMarkupContent(12, "" "")
                __builder0.AddMarkupContent(13, ""times"")
                __builder0.AddMarkupContent(14, ""!"")
                __builder0.CloseElement()
            end
    end
    local count = 0
    local function onClick()
        count = count + 1
    end

    function self:setParameters(p)
        self.parameters = p
    end
    function self:build(__builder0)
        __builder0.AddMarkupContent(0, ""\n"")
        __builder0.OpenElement(1, ""div"")
        __builder0.AddAttribute(2, ""style"", (""height:200px;width:200px;background:green;color:white;padding:20px""))
        __builder0.AddAttribute(3, ""onclick"", (onClick))
        __builder0.AddMarkupContent(4, ""\n    "")
        __builder0.AddContent(5, (x))
        __builder0.AddMarkupContent(6, ""\n    "")
        __builder0.AddContent(7, (y(count)))
        __builder0.AddMarkupContent(8, ""\n"")
        __builder0.CloseElement()
    end
    return self
end
";
        public static void Main()
        {
            var (sig1, _) = StackSignature.CreateUnspecialized(1);
            var (sig2, _) = StackSignature.CreateUnspecialized(2);
            var proto2 = new Proto
            {
                ChildFunctions = Array.Empty<Proto>(),
                ConstantsU = new TypedValue[]
                {
                    TypedValue.MakeDouble(1),
                },
                SigDesc = new SignatureDesc[]
                {
                    sig1.GetDesc(),
                },
                ParameterSig = StackSignature.Empty.GetDesc(),
                NumStackSize = 100,
                ObjStackSize = 100,
                LocalRegionOffsetO = 0,
                LocalRegionOffsetV = 0,
                UpvalRegionOffset = 0,
                SigRegionOffsetO = 10,
                SigRegionOffsetV = 10,
                Instructions = new uint[]
                {
                    (uint)(Opcodes.K) << 24 | 2 << 16 | 0 << 8 | 0, //local[2] = constant[0]
                    (uint)(Opcodes.RETN) << 24 | 0 << 16 | 2 << 8 | 2, //return sig:0 (2,2)
                },
            };
            var closure2 = new LClosure
            {
                Proto = proto2,
                UpvalLists = Array.Empty<TypedValue[]>(),
            };
            var proto1 = new Proto
            {
                ChildFunctions = Array.Empty<Proto>(),
                ConstantsU = new TypedValue[]
                {
                    TypedValue.MakeDouble(1),
                    TypedValue.MakeDouble(2),
                    TypedValue.MakeLClosure(closure2),
                },
                SigDesc = new SignatureDesc[]
                {
                    sig2.GetDesc(),
                    StackSignature.Empty.GetDesc(),
                    sig1.GetDesc(),
                },
                ParameterSig = StackSignature.Empty.GetDesc(),
                NumStackSize = 100,
                ObjStackSize = 100,
                LocalRegionOffsetO = 0,
                LocalRegionOffsetV = 0,
                UpvalRegionOffset = 0,
                SigRegionOffsetO = 10,
                SigRegionOffsetV = 10,
                Instructions = new uint[]
                {
                    (uint)(Opcodes.K) << 24 | 9 << 16 | 2 << 8 | 0, //local[9] = constant[2]
                    (uint)(Opcodes.CALLC) << 24 | 9 << 16 | 1 << 8 | 2, //call local[9] sig: 1(sig0)->2(sig1) (10,10)
                    (uint)(Opcodes.K) << 24 | 1 << 16 | 1 << 8 | 0, //local[1] = constant[1]
                    (uint)(Opcodes.ADD) << 24 | 10 << 16 | 10 << 8 | 1, //add local[10] = local[10] + local[1]
                    (uint)(Opcodes.RETN) << 24 | 2 << 16 | 10 << 8 | 10, //ret sig: 2(sig1) (10,10)
                },
            };
            var closure1 = new LClosure
            {
                Proto = proto1,
                UpvalLists = Array.Empty<TypedValue[]>(),
            };
            var thread = new Thread();
            var clock = Stopwatch.StartNew();
            var stack = thread.Stack.Allocate(20, 20);
            var emptySig = SignatureDesc.Empty;
            for (int i = 0; i < 10000000; ++i)
            {
                thread.SetSigBlock(ref emptySig, 0, 0);
                LuaInterpreter.Execute(thread, closure1, ref stack);
            }
            Console.WriteLine(clock.ElapsedMilliseconds);
        }

        public static void AST()
        {
            for (int i = 0; i < 1000000; ++i)
            {
                var codeReader = new StringReader(_code3);
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
                //ast.Dump(Console.Out);

                //Console.WriteLine();
                //Console.WriteLine("===============");
                //Console.WriteLine();

                var stream = new MemoryStream();
                var writer = new BinaryWriter(stream);
                ast.Write(writer);

                stream.Seek(0, SeekOrigin.Begin);
                var reader = new BinaryReader(stream);
                var newTree = SyntaxRoot.Read(reader);
            }
            //newTree.Dump(Console.Out);
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
                            Int32Value = 123,
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
