using FastLua.SyntaxTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    internal class BlockGenerator : StatementGenerator
    {
        public readonly GroupStackFragment Stack;
        public readonly BlockStackFragment LocalStack;
        public readonly TempVarAllocator TempAllocator;
        private readonly List<StatementGenerator> _generators = new();

        public BlockGenerator(GeneratorFactory factory, GroupStackFragment parentStack, BlockSyntaxNode block)
        {
            Stack = new();
            LocalStack = new();
            var tempStack = new BlockStackFragment();
            TempAllocator = new(tempStack);
            Stack.Add(LocalStack);
            Stack.Add(tempStack);
            parentStack.Add(Stack);
            foreach (var variable in block.LocalVariables)
            {
                var expr = new LocalVariableExpressionGenerator(LocalStack, variable.Target);
                factory.Function.Locals.Add(variable.Target, expr);
            }
            foreach (var statement in block.Statements)
            {
                _generators.Add(factory.CreateStatement(this, statement));
            }
        }

        public override void Emit(InstructionWriter writer)
        {
            foreach (var g in _generators)
            {
                g.Emit(writer);
            }
        }
    }
}
