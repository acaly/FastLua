using FastLua.SyntaxTree;
using FastLua.VM;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    public sealed class CodeGenerator
    {
        private readonly SignatureManager _sigManager = new();
        
        public LClosure Compile(SyntaxRoot ast, Table env)
        {
            Proto CompileFunction(ulong id)
            {
                //TODO should add root function to Functions list.
                if (id == ast.RootFunction.GlobalId)
                {
                    var funcGen = new FunctionGenerator(_sigManager);
                    return funcGen.Generate(ast.RootFunction, CompileFunction);
                }
                else
                {
                    var func = ast.Functions.Single(func => func.GlobalId == id);
                    var funcGen = new FunctionGenerator(_sigManager);
                    return funcGen.Generate(func, CompileFunction);
                }
            }

            var proto = CompileFunction(ast.RootFunction.GlobalId);
            var upvalLists = Array.Empty<TypedValue[]>();
            if (ast.RootFunction.ImportedUpValueLists.Count != 0)
            {
                Debug.Assert(ast.RootFunction.ImportedUpValueLists.Count == 1);
                Debug.Assert(ast.RootFunction.ImportedUpValueLists[0].Variables.Count == 1);
                upvalLists = new TypedValue[1][]
                {
                    new TypedValue[] { TypedValue.MakeTable(env) },
                };
            }

            return new LClosure
            {
                Proto = proto,
                UpvalLists = upvalLists,
            };
        }
    }
}
