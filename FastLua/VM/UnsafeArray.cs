using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    //Wrapper for unmanaged pointer.
    //Note: this is a non-copyable, immutable struct.
    internal readonly unsafe struct UnsafeArray<T> where T : unmanaged
    {
        private readonly T* _data;
        public int Length { get; private init; }

        public UnsafeArray(int length)
        {
            Length = length;
            //Allocate unmanaged memory with alignment of 8 bytes.
            var ptr = (nint)Marshal.AllocHGlobal(length * sizeof(T) + 8);
            ptr = (ptr + 7) & ~7;
            _data = (T*)ptr;
        }

        public readonly ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _data[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Span<T> AsSpan()
        {
            return MemoryMarshal.CreateSpan(ref _data[0], Length);
        }

        internal T* GetPointer(int index)
        {
            return _data + index;
        }

        public static UnsafeArray<T> Create(params T[] data)
        {
            var ret = new UnsafeArray<T>(data.Length);
            data.AsSpan().CopyTo(ret.AsSpan());
            return ret;
        }

        public static void Free(UnsafeArray<T> array)
        {
            Marshal.FreeHGlobal((IntPtr)array._data);
        }

        public static readonly UnsafeArray<T> Null = default;
    }

    internal readonly struct ReadOnlyUnsafeArray<T> where T : unmanaged
    {
        private readonly UnsafeArray<T> _array;

        public ReadOnlyUnsafeArray(UnsafeArray<T> array)
        {
            _array = array;
        }

        public readonly ref readonly T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _array[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ReadOnlySpan<T> AsSpan()
        {
            return _array.AsSpan();
        }
    }
}
