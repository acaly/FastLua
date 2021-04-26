using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    internal sealed unsafe class UnsafeStorage<T> : IDisposable where T : unmanaged
    {
        private readonly int _increaseSize;
        private readonly List<UnsafeArray<T>> _arrays = new();
        private int _lastArrayCount = 0;

        public UnsafeStorage(int initialSize = 10, int increaseSize = 1)
        {
            _arrays.Add(new(initialSize));
            _increaseSize = increaseSize;
        }

        ~UnsafeStorage()
        {
            DisposeArrays();
        }

        public void Dispose()
        {
            DisposeArrays();
            GC.SuppressFinalize(this);
        }

        private void DisposeArrays()
        {
            foreach (var a in _arrays)
            {
                UnsafeArray<T>.Free(a);
            }
            _arrays.Clear();
        }

        public T* Get()
        {
            if (_lastArrayCount >= _arrays[^1].Length)
            {
                _arrays.Add(new(_arrays[^1].Length * _increaseSize));
                _lastArrayCount = 0;
            }
            return _arrays[^1].GetPointer(_lastArrayCount++);
        }
    }
}
