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
        private readonly BlockStackFragment _stack;

        public TempVarAllocator(BlockStackFragment stack)
        {
            _stack = stack;
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
    }
}
