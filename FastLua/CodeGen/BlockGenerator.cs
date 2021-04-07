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
    internal class BlockGenerator : StatementGenerator
    {
        public readonly GroupStackFragment Stack;
        public readonly BlockStackFragment UpvalStack;
        public readonly BlockStackFragment LocalStack;
        public readonly TempVarAllocator TempAllocator;
        private readonly List<(AllocatedLocal stack, int count)> _upvalInit = new();
        private readonly List<StatementGenerator> _generators = new();

        public BlockGenerator(GeneratorFactory factory, GroupStackFragment parentStack, BlockSyntaxNode block)
        {
            Stack = new();
            UpvalStack = new();
            LocalStack = new();
            var tempStack = new BlockStackFragment();
            TempAllocator = new(tempStack);
            Stack.Add(UpvalStack);
            Stack.Add(LocalStack);
            Stack.Add(tempStack);
            parentStack.Add(Stack);
            foreach (var upList in block.UpValueLists)
            {
                var listLocal = UpvalStack.AddObject();
                _upvalInit.Add((listLocal, upList.Variables.Count));
                for (int i = 0; i < upList.Variables.Count; ++i)
                {
                    var upvalGenerator = new UpvalueExpressionGenerator(listLocal, i, upList.Variables[i].Target);
                    factory.Function.Locals.Add(upList.Variables[i].Target, upvalGenerator);
                }
            }
            foreach (var variable in block.LocalVariables)
            {
                if (variable.Target.ExportUpValueList is not null)
                {
                    //Already added as upval.
                    Debug.Assert(block.UpValueLists.Contains(variable.Target.ExportUpValueList.Target));
                    continue;
                }
                var expr = new LocalVariableExpressionGenerator(LocalStack, variable.Target);
                factory.Function.Locals.Add(variable.Target, expr);
            }
            foreach (var statement in block.Statements)
            {
                _generators.Add(factory.CreateStatement(this, statement));
                factory.Function.CheckStatementState();
            }
        }

        public override void Emit(InstructionWriter writer)
        {
            foreach (var (stack, count) in _upvalInit)
            {
                if (stack.Offset > 255 || count > 255)
                {
                    throw new NotImplementedException();
                }
                writer.WriteUUU(Opcodes.UNEW, stack.Offset, count, 0);
            }
            foreach (var g in _generators)
            {
                g.Emit(writer);
            }
        }
    }
}
