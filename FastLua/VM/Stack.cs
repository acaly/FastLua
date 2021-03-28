using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace FastLua.VM
{
    internal sealed class StackSegment
    {
        public StackFrameList<double> NumberStack;
        public StackFrameList<object> ObjectStack;
    }

    internal ref struct StackFrame
    {
        //In order for the linked list to work. This also stores index of StackSegment.
        public nint Head;
        public ReadOnlySpan<nint> Last; //Last StackFrame's Head field.
        public Span<double> NumberFrame;
        public Span<object> ObjectFrame;

        //TODO other fields for the current frame
    }

    internal struct SerializedStackFrame
    {
        public ArraySegment<double> NumberFrame;
        public ArraySegment<object> ObjectFrame;

        //TODO other fields for the current frame
    }

    internal sealed class StackManager
    {
        public readonly List<StackSegment> Segments = new();

        public StackFrame Allocate(int numSize, int objSize, ref StackFrame lastFrame)
        {
            Debug.Assert(numSize < Options.MaxSingleFunctionStackSize);
            Debug.Assert(objSize < Options.MaxSingleFunctionStackSize);
            throw new NotImplementedException();
            //If enough, use last segment; otherwise create a new segment
        }

        public void Deallocate(ref StackFrame lastFrame)
        {
            throw new NotImplementedException();
            //Pop from last segment.
            //Remove last segment (move to cache) if it's empty
        }

        public void SerializeAll(ref StackFrame lastFrame)
        {
            throw new NotImplementedException();
        }

        public StackFrame DeserializeSingle()
        {
            throw new NotImplementedException();
        }
    }
}
