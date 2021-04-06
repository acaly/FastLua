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
    internal abstract class ExpressionGenerator
    {
        private readonly AllocatedLocal _null;

        protected ExpressionGenerator(GeneratorFactory factory)
        {
            _null = factory.Function.NullSlot;
        }

        //Use a explicit ctor to prevent derived class to forget about the _null.
        protected ExpressionGenerator(int _)
        {
        }

        public abstract bool TryGetSingleType(out VMSpecializationType type);

        public VMSpecializationType GetSingleType()
        {
            var hasSingleType = TryGetSingleType(out var ret);
            Debug.Assert(hasSingleType);
            return ret;
        }

        //For all expressions: write its type to writer. Vararg expr should write vararg.
        //This is called before the emit stage.
        public virtual void WritSig(SignatureWriter writer)
        {
            writer.AppendFixed(GetSingleType());
        }

        //For locals & arguments: provide the slot it occupies on stack.
        //This might be called before and during the emit stage.
        //EmitPrep will NOT be called before this.
        public abstract bool TryGetFromStack(out AllocatedLocal stackOffset);

        //For index variable: calculate table and key and write to stack.
        public virtual void EmitPrep(InstructionWriter writer)
        {
        }

        //For all expressions: get the single value and write to dest.
        //EmitPrep will be called before this.
        public abstract void EmitGet(InstructionWriter writer, AllocatedLocal dest);

        //For vararg expr: calculate the value and write to stack at sigblock's location, with the given sig index.
        //The sigIndex is the index into proto's signature list. This should be the out signature of function calls.
        //EmitPrep will be called before this.
        public virtual void EmitGet(InstructionWriter writer, SequentialStackFragment sigBlock, int sigIndex)
        {
            throw new NotSupportedException();
        }

        //For variables: set the value of the variable to the value at src.
        //EmitPrep will be called before this.
        public virtual void EmitSet(InstructionWriter writer, AllocatedLocal src, VMSpecializationType type)
        {
            throw new NotSupportedException();
        }

        //For expressions with side effects: perform the expression's side effect without saving the value.
        //EmitPrep will NOT be called before this.
        public virtual void EmitDiscard(InstructionWriter writer)
        {
            EmitPrep(writer);
            EmitGet(writer, _null);
        }
    }
}
