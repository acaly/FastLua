using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace FastLua.VM
{
    internal readonly ref struct StackFrameValues
    {
        public readonly Span<TypedValue> Span;

        public StackFrameValues(Span<TypedValue> span)
        {
            Span = span;
        }

        public readonly ref TypedValue this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.Add(ref MemoryMarshal.GetReference(Span), index);
        }

        public readonly ref TypedValue GetUpvalue(int stackOffset, int index)
        {
            return ref ((TypedValue[])this[stackOffset].Object)[index];
        }
    }

    //This struct stores other per-frame data (in addition to stack).
    internal struct StackFrameVarargInfo
    {
        //Storage of the function vararg list (in VM's state.VarargStack).

        public int VarargStart;
        public int VarargLength;
    }

    internal struct StackFrame
    {
        public int Segment;
        public int Offset;
        public int Length;

        public bool ForceOnSameSegment;
        public bool ActualOnSameSegment;

        public int PC;
        public int SigOffset;

        public StackFrameVarargInfo VarargInfo;
    }
}
