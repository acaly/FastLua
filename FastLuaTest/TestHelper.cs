using FastLua.CodeGen;
using FastLua.Parser;
using FastLua.VM;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLuaTest
{
    internal static class TestHelper
    {
        private static LClosure Compile(string code)
        {
            var codeReader = new StringReader(code);
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
            return codeGen.Compile(ast, null);
        }

        public static void DoString(string str, Span<TypedValue> args, Span<TypedValue> results)
        {
            var closure = Compile(str);

            var thread = new Thread();
            var stackSize = Math.Max(args.Length, results.Length);
            var stack = thread.AllocateCSharpStack(stackSize);
            if (args.Length > 0)
            {
                throw new NotImplementedException();
            }
            stack.Write(0, args);

            //TODO use stack.Read
            var ret = new List<TypedValue>();
            LuaInterpreter.Execute(thread, closure, stack, ret);
            var copyRetCount = Math.Min(ret.Count, results.Length);
            for (int i = 0; i < copyRetCount; ++i)
            {
                results[i] = ret[i];
            }
            for (int i = copyRetCount; i < results.Length; ++i)
            {
                results[i] = TypedValue.Nil;
            }
        }
    }
}
