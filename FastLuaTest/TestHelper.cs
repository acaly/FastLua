using FastLua.CodeGen;
using FastLua.Parser;
using FastLua.VM;
using NUnit.Framework;
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
        public static readonly Table AssertEnv;
        public static readonly Table DefaultEnv;

        static TestHelper()
        {
            AssertEnv = new Table();
            AssertEnv.SetRaw(TypedValue.MakeString("assert"), TypedValue.MakeNClosure(AssertFunc));

            DefaultEnv = new Table();
            DefaultEnv.SetRaw(TypedValue.MakeString("assert"), TypedValue.MakeNClosure(AssertFunc));
            DefaultEnv.SetRaw(TypedValue.MakeString("add"), TypedValue.MakeNClosure(AddFunc));
        }

        private static int AssertFunc(StackInfo stack, int args)
        {
            Assert.AreEqual(1, args);
            stack.Read(0, out var val);
            Assert.AreEqual(LuaValueType.Bool, val.ValueType);
            Assert.IsTrue(val.BoolVal);
            return 0;
        }

        private static int AddFunc(StackInfo stack, int args)
        {
            Assert.AreEqual(2, args);
            stack.Read(0, out var a);
            stack.Read(1, out var b);
            Assert.AreEqual(LuaValueType.Number, a.ValueType);
            Assert.AreEqual(LuaValueType.Number, b.ValueType);
            stack.Write(0, TypedValue.MakeDouble(a.NumberVal + b.NumberVal));
            return 1;
        }

        private static LClosure Compile(string code, Table env)
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
            return codeGen.Compile(ast, env);
        }

        public static void DoString(string str, Table env, Span<TypedValue> args, Span<TypedValue> results)
        {
            var closure = Compile(str, env);

            var thread = new Thread();
            var stackSize = Math.Max(args.Length, results.Length);
            var stack = thread.AllocateRootCSharpStack(stackSize);
            if (args.Length > 0)
            {
                throw new NotImplementedException();
            }
            stack.Write(0, args);

            var retCount = LuaInterpreter.Execute(thread, closure, stack, 0, 0);
            stack.Read(0, results);
            for (int i = retCount; i < results.Length; ++i)
            {
                results[i] = TypedValue.Nil;
            }
        }
    }
}
