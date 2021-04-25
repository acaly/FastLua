using FastLua.CodeGen;
using FastLua.Parser;
using FastLua.SyntaxTree;
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
            DefaultEnv.SetRaw(TypedValue.MakeString("next"), TypedValue.MakeNClosure(NextFunc));
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

        private static int NextFunc(StackInfo stack, int args)
        {
            Assert.GreaterOrEqual(args, 1);
            Assert.LessOrEqual(args, 2);
            TypedValue table, key = TypedValue.Nil;
            stack.Read(0, out table);
            if (args == 2)
            {
                stack.Read(1, out key);
            }
            Assert.AreEqual(LuaValueType.Table, table.ValueType);
            table.TableVal.Next(ref key, out var val);
            stack.Write(0, key);
            stack.Write(1, val);
            return 2;
        }

        private static void CompareProto(Proto p1, Proto p2)
        {
            Assert.AreEqual(p1.Instructions, p2.Instructions);
            Assert.AreEqual(p1.Constants, p2.Constants);
            //TODO compare other fields
        }

        private static void CheckASTSerialization(SyntaxRoot ast, LClosure closure)
        {
            using var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            ast.Write(writer);
            stream.Position = 0;
            var reader = new BinaryReader(stream);
            var newAst = SyntaxRoot.Read(reader);

            var codeGen = new CodeGenerator();
            var newClosure = codeGen.Compile(ast, null);

            CompareProto(closure.Proto, newClosure.Proto);
        }

        public static LClosure Compile(string code, Table env)
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
            var ret = codeGen.Compile(ast, env);
            CheckASTSerialization(ast, ret);

            return ret;
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

            var retCount = LuaInterpreter.Execute(stack, closure, 0, 0);
            stack.Read(0, results);
            for (int i = retCount; i < results.Length; ++i)
            {
                results[i] = TypedValue.Nil;
            }
        }
    }
}
