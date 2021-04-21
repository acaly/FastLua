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

    public struct StackInfo
    {
        internal Thread Thread;
        internal int StackFrame;

        public void Write(int start, ReadOnlySpan<TypedValue> values)
        {
            var frameValues = Thread.GetFrameValues(ref Thread.GetFrame(StackFrame));
            values.CopyTo(frameValues.Span[start..]);
        }

        public void Write(int start, in TypedValue values)
        {
            Write(start, MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in values), 1));
        }

        public void Read(int start, Span<TypedValue> values)
        {
            var frameValues = Thread.GetFrameValues(ref Thread.GetFrame(StackFrame));
            var copyLen = Math.Min(frameValues.Span.Length - start, values.Length);
            frameValues.Span.Slice(start, copyLen).CopyTo(values);
        }

        public void Read(int start, out TypedValue value)
        {
            value = TypedValue.Nil;
            Read(start, MemoryMarshal.CreateSpan(ref value, 1));
        }

        public void Reallocate(int newSize)
        {
            Thread.ReallocateFrameInternal(ref Thread.GetFrame(StackFrame), newSize);
        }
    }

    [Obsolete("Use StackSignatureState")]
    internal struct SignatureDesc
    {
        public ulong SigTypeId;
        public StackSignature SigType;
        public int SigFLength; //Length of fixed part.
        public bool HasV;

        public static readonly SignatureDesc Null = StackSignature.Null.GetDesc();
        public static readonly SignatureDesc Empty = StackSignature.Empty.GetDesc();
        public static readonly SignatureDesc EmptyV = StackSignature.EmptyV.GetDesc();

        public SignatureDesc WithVararg(VMSpecializationType type)
        {
            if (HasV)
            {
                throw new InvalidOperationException();
            }
            var sig = SigType.WithVararg(type);
            var (vv, vo) = type.GetStorageType();
            return new SignatureDesc
            {
                SigTypeId = sig.GlobalId,
                SigType = sig,
                SigFLength = SigFLength,
                HasV = vo || vv,
            };
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
        public int LastWrite;
        public int SigOffset;
        public int RetSigIndex;

        public StackFrameVarargInfo VarargInfo;
    }
}
