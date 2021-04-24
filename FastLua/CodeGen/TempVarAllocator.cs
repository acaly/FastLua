using FastLua.SyntaxTree;
using FastLua.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    internal class TempVarAllocator
    {
        private readonly OverlappedStackFragment _mergedStack;
        private BlockStackFragment _stack;

        public TempVarAllocator(OverlappedStackFragment stack)
        {
            _mergedStack = stack;

            _stack = new();
            _mergedStack.Add(_stack);
        }

        public AllocatedLocal Allocate(ExpressionSyntaxNode expr)
        {
            //Allocate a new slot for each temp var.
            var type = expr.SpecializationType.GetVMSpecializationType();
            return Allocate(type);
        }

        public AllocatedLocal Allocate(VMSpecializationType type)
        {
            //Allocate a new slot for each temp var.
            return _stack.AddSpecializedType(type);
        }

        public void Reset()
        {
            _stack = new();
            _mergedStack.Add(_stack);
        }
    }
}
