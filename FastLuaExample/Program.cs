using FastLua.CodeGen;
using FastLua.Parser;
using FastLua.VM;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace FastLuaExample
{
    public class Program
    {
        private static readonly string _code = @"local name = (...) print('Hello, ' .. name .. '!')";

        private static int Print(StackInfo stackInfo, int argSize)
        {
            stackInfo.Read(0, out var value);
            if (value.ValueType == LuaValueType.String)
            {
                Console.WriteLine($"{value.StringVal}");
            }
            else
            {
                Console.WriteLine($"[{value.ValueType}]");
            }
            return 0;
        }

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

            var env = new Table();
            env.SetRaw(TypedValue.MakeString("print"), TypedValue.MakeNClosure(Print));

            var codeGen = new CodeGenerator();
            var closure = codeGen.Compile(ast, env);

            var thread = new Thread();
            var stack = thread.AllocateRootCSharpStack(1);

            var arg = TypedValue.MakeString("C#");

            stack.Write(0, in arg);
            LuaInterpreter.Execute(stack, closure, 0, 1);
            stack.Read(0, out var ret);
        }
    }
}
