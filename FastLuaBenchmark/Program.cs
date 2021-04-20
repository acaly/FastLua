using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using FastLua.CodeGen;
using FastLua.Parser;
using FastLua.VM;
using System.Collections.Generic;
using System.IO;

namespace FastLuaBenchmark
{
    [SimpleJob]
    public class Program
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

        private void PrepareScripts(string name)
        {
            var code = File.ReadAllText(name + ".lua");
            _fastLuaClosure = Compile(code);
            _moonSharpClosure = new MoonSharp.Interpreter.Script().LoadString(code).Function;
            global::KopiLua.Lua.luaL_loadstring(_kopiLuaState, new(code));
        }

        //[Params("self_function", "numeric_for")]
        [Params("recursive_call")]
        public string ScriptFile
        {
            set => PrepareScripts(value);
        }

        private readonly Thread _fastLuaThread = new();
        private readonly List<TypedValue> _fastLuaRetList = new();
        private LClosure _fastLuaClosure;

        private MoonSharp.Interpreter.Closure _moonSharpClosure;

        private readonly KopiLua.Lua.lua_State _kopiLuaState = global::KopiLua.Lua.luaL_newstate();

        [Benchmark]
        public double FastLua()
        {
            var stack = _fastLuaThread.AllocateCSharpStack(1);
            stack.Write(0, default);
            LuaInterpreter.Execute(_fastLuaThread, _fastLuaClosure, stack, _fastLuaRetList);
            return _fastLuaRetList[0].NumberVal;
        }

        //[Benchmark(Baseline = true)]
        public double MoonSharp()
        {
            return _moonSharpClosure.Call().Number;
        }

        //[Benchmark]
        public double KopiLua()
        {
            global::KopiLua.Lua.lua_pushvalue(_kopiLuaState, -1);
            global::KopiLua.Lua.lua_call(_kopiLuaState, 0, 1);
            var r = global::KopiLua.Lua.lua_tonumber(_kopiLuaState, -1);
            global::KopiLua.Lua.lua_pop(_kopiLuaState, 1);
            return r;
        }

        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                args = new[]
                {
                    "--filter", "Program",
                    "--maxIterationCount", "20",
                    "--iterationTime", "1000",
                };
            }
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new CustomConfig());
        }
    }
}
