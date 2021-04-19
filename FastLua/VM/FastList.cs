using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    //Note: this is a non-copyable, mutable struct.
    internal struct FastList<T> where T : unmanaged
    {
        private T[] _data;
        private int _count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureSpace(int count)
        {
            Debug.Assert(_data is not null && _data.Length > 0);
            if (_count + count > _data.Length)
            {
                var newCount = _data.Length * 2;
                while (newCount < _count + count)
                {
                    newCount *= 2;
                }
                var newData = new T[newCount];
                Array.Copy(_data, newData, _count);
                _data = newData;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureSpace()
        {
            Debug.Assert(_data is not null && _data.Length > 0);
            if (_count + 1 > _data.Length)
            {
                var newData = new T[_data.Length * 2];
                Array.Copy(_data, newData, _count);
                _data = newData;
            }
        }

        public void Init(int capacity)
        {
            _data = new T[capacity];
        }

        public ref T Add()
        {
            EnsureSpace();
            return ref _data[_count++];
        }

        public Span<T> Add(int count)
        {
            EnsureSpace(count);
            var offset = _count;
            _count += count;
            return new(_data, offset, count);
        }

        public void RemoveLast()
        {
            _count -= 1;
        }

        public void RemoveLast(int index)
        {
            Debug.Assert(_count == index + 1 && index >= 0);
            _count -= 1;
        }
    }
}
