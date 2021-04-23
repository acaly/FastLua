using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    public delegate int NativeFunctionDelegate(StackInfo stackInfo, int argSize);
    public delegate ValueTask<int> AsyncNativeFunctionDelegate(AsyncStackInfo stackInfo, int argSize);

    public ref struct StackInfo
    {
        internal AsyncStackInfo AsyncStackInfo;
        internal StackFrameValues Values;

        public StackInfo(AsyncStackInfo asyncStackInfo)
        {
            AsyncStackInfo = asyncStackInfo;
            Values.Span = AsyncStackInfo.GetFrameValues();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Write(int start, ReadOnlySpan<TypedValue> values)
        {
            values.CopyTo(Values.Span[start..]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Write(int start, in TypedValue value)
        {
            Values.Span[start] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Read(int start, Span<TypedValue> values)
        {
            var copyLen = Math.Min(Values.Span.Length - start, values.Length);
            Values.Span.Slice(start, copyLen).CopyTo(values);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Read(int start, out TypedValue value)
        {
            value = Values.Span[start];
        }

        public void Reallocate(int newSize)
        {
            AsyncStackInfo.Reallocate(newSize);
            Values.Span = AsyncStackInfo.GetFrameValues();
        }
    }

    public struct AsyncStackInfo
    {
        internal readonly Thread Thread;
        internal int StackFrame;

        internal AsyncStackInfo(Thread thread, int frame)
        {
            Thread = thread;
            StackFrame = frame;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly Span<TypedValue> GetFrameValues()
        {
            return Thread.GetFrameValues(in Thread.GetFrame(StackFrame));
        }

        public readonly void Write(int start, ReadOnlySpan<TypedValue> values)
        {
            values.CopyTo(GetFrameValues()[start..]);
        }

        public readonly void Write(int start, in TypedValue values)
        {
            Write(start, MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in values), 1));
        }

        public readonly void Read(int start, Span<TypedValue> values)
        {
            var frameValues = GetFrameValues();
            var copyLen = Math.Min(frameValues.Length - start, values.Length);
            frameValues.Slice(start, copyLen).CopyTo(values);
        }

        public readonly void Read(int start, out TypedValue value)
        {
            value = TypedValue.Nil;
            Read(start, MemoryMarshal.CreateSpan(ref value, 1));
        }

        public readonly void Reallocate(int newSize)
        {
            Thread.ReallocateFrameInternal(ref Thread.GetFrame(StackFrame), newSize);
        }
    }

    //Used as Target object for wrapped functions.
    internal class WrappedNativeFunctionInfo
    {
        public object RawDelegate;
    }
}
