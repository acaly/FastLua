using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace FastLua.VM
{
    internal ref struct StackFrameValues
    {
        public Span<TypedValue> Span;

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

        public StackFrameRef Next;
        public StackFrameRef Prev;
        public int Index;
    }

    internal unsafe readonly struct StackFrameRef : IEquatable<StackFrameRef>
    {
        private readonly StackFrame* _ptr;

        public StackFrameRef(StackFrame* ptr)
        {
            _ptr = ptr;
        }

        public ref StackFrame Data
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *_ptr;
        }

        public bool IsNull => _ptr == null;

        public override bool Equals(object obj)
        {
            return obj is StackFrameRef @ref && Equals(@ref);
        }

        public bool Equals(StackFrameRef other)
        {
            return EqualityComparer<IntPtr>.Default.Equals((IntPtr)_ptr, (IntPtr)other._ptr);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((IntPtr)_ptr);
        }

        public static bool operator ==(StackFrameRef left, StackFrameRef right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StackFrameRef left, StackFrameRef right)
        {
            return !(left == right);
        }
    }
}
