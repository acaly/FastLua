using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using FastLua.CodeGen;
using FastLua.Parser;
using FastLua.VM;
using KopiLua;
using MoonSharp.Interpreter;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

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

            return _fastLuaCodeGen.Compile(ast, _fastLuaEnv);
        }

        private void PrepareScripts(string name)
        {
            var code = File.ReadAllText(name + ".lua");
            _fastLuaClosure = Compile(code);
            _moonSharpClosure = _moonSharpScript.LoadString(code).Function;
            Lua.luaL_loadstring(_kopiLuaState, new(code));
        }

        [Params("self_function", "numeric_for", "recursive_call", "native_call")]
        public string ScriptFile
        {
            set => PrepareScripts(value);
        }

        private static readonly CodeGenerator _fastLuaCodeGen = new();
        private static readonly FastLua.VM.Table _fastLuaEnv = new();
        private readonly Thread _fastLuaThread = new();
        private readonly AsyncStackInfo _fastLuaStackFrame;
        private LClosure _fastLuaClosure;

        private readonly Script _moonSharpScript = new();
        private Closure _moonSharpClosure;

        private readonly Lua.lua_State _kopiLuaState = Lua.luaL_newstate();

        public Program()
        {
            _fastLuaEnv.SetRaw(TypedValue.MakeString("add"), TypedValue.MakeNClosure(FastLuaNativeAdd));
            _fastLuaStackFrame = _fastLuaThread.AllocateRootCSharpStack(1);

            _moonSharpScript.Globals.Set("add", DynValue.NewCallback(MoonSharpNativeAdd));

            Lua.lua_pushcfunction(_kopiLuaState, KopiLuaNativeAdd);
            Lua.lua_setglobal(_kopiLuaState, "add");
        }

        private static int FastLuaNativeAdd(StackInfo stack, int args)
        {
            stack.Read(0, out var a);
            stack.Read(1, out var b);
            stack.Write(0, TypedValue.MakeDouble(a.NumberVal + b.NumberVal));
            return 1;
        }

        private static DynValue MoonSharpNativeAdd(ScriptExecutionContext context, CallbackArguments args)
        {
            return DynValue.NewNumber(args[0].Number + args[1].Number);
        }

        private static int KopiLuaNativeAdd(Lua.lua_State L)
        {
            var a = Lua.lua_tonumber(L, 1);
            var b = Lua.lua_tonumber(L, 2);
            Lua.lua_pushnumber(L, a + b);
            return 1;
        }

        [Benchmark(Baseline = true)]
        public double FastLua()
        {
            LuaInterpreter.Execute(_fastLuaStackFrame, _fastLuaClosure, 0, 0);
            _fastLuaStackFrame.Read(0, out var ret);
            return ret.NumberVal;
        }

        [Benchmark]
        public double MoonSharp()
        {
            return _moonSharpClosure.Call().Number;
        }

        [Benchmark]
        public double KopiLua()
        {
            Lua.lua_pushvalue(_kopiLuaState, -1);
            Lua.lua_call(_kopiLuaState, 0, 1);
            var r = Lua.lua_tonumber(_kopiLuaState, -1);
            Lua.lua_pop(_kopiLuaState, 1);
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
                    "--iterationTime", "200",
                };
            }
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new CustomConfig());
        }
    }
}
