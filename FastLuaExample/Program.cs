using FastLua.CodeGen;
using FastLua.Parser;
using FastLua.VM;
using System;
using System.Diagnostics;
using System.IO;

namespace FastLuaExample
{
    public class Program
    {
        private static readonly string _code = @"
local a = 0
for i = 1, 5 do
	a = a + i
end
return a";

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

            var codeGen = new CodeGenerator();
            var closure = codeGen.Compile(ast, null);

            var thread = new Thread();
            var stack = thread.AllocateRootCSharpStack(1);

            var clock = Stopwatch.StartNew();
            for (int i = 0; i < 10000000; ++i)
            {
                thread.ClearSigBlock();
                LuaInterpreter.Execute(thread, closure, stack, null);
            }
            Console.WriteLine(clock.ElapsedMilliseconds);
        }
    }
}
