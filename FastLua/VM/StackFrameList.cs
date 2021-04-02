﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace FastLua.VM
{
    internal struct StackFrameList<T>
    {
        private static readonly int _size = typeof(T).IsValueType ? Marshal.SizeOf<T>() : IntPtr.Size;

        private readonly T[] _data;
        private int _count;

        public StackFrameList(int capacity)
        {
            _data = new T[capacity];
            _count = 0;
        }

        public void CopyTo(StackFrameList<T> newStack)
        {
            Debug.Assert(newStack._data.Length >= _data.Length);
            _data.CopyTo(newStack._data, 0);
        }

        public int GetIndex(ref T val)
        {
            return (int)Unsafe.ByteOffset(ref _data[0], ref val) / _size;
        }

        public Span<T> FromIndex(int index, int length)
        {
            return _data.AsSpan(index, length);
        }

        public Span<T> Push(int newSize)
        {
            if (!CheckSpace(_count, newSize))
            {
                throw new InvalidOperationException();
            }
            var span = MemoryMarshal.CreateSpan(ref _data[_count], newSize);
            _count += newSize;
            return span;
        }

        //public void Pop(Span<T> span)
        //{
        //    if (GetIndex(ref span[0]) + span.Length != _count)
        //    {
        //        throw new InvalidOperationException("Stack order check failed.");
        //    }
        //    _count -= span.Length;
        //}

        public void Clear()
        {
            _count = 0;
        }

        public void Reset(Span<T> lastSpan)
        {
            _count = GetIndex(ref lastSpan[0]) + lastSpan.Length;
        }

        public bool CheckSpace(int size)
        {
            return CheckSpace(_count, size);
        }

        private bool CheckSpace(int start, int newSize)
        {
            return _data.Length >= start + newSize;
        }

        public bool CheckSpace(ref Span<T> span, int newSize)
        {
            return CheckSpace(GetIndex(ref span[0]), newSize);
        }

        public bool TryExtend(ref Span<T> span, int newSize)
        {
            if (!CheckSpace(ref span, newSize))
            {
                return false;
            }
            span = MemoryMarshal.CreateSpan(ref span[0], newSize);
            _count = GetIndex(ref span[0]) + span.Length;
            return true;
        }

        public ArraySegment<T> ToArraySegment(Span<T> span)
        {
            return new(_data, GetIndex(ref span[0]), span.Length);
        }
    }
}
