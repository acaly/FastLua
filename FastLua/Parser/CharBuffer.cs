using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.Parser
{
    //Similar to StringBuilder but use single array to store entire string,
    //which allows getting the content as a span.
    //Note that this is a mutable struct.
    public struct CharBuffer<T> where T : unmanaged
    {
        private static readonly int _size = Unsafe.SizeOf<T>();
        private T[] _buffer;
        private int _length;

        //Most of the cases we are only reading. Use ReadOnlySpan to avoid mistakes.
        public ReadOnlySpan<T> Content
        {
            get => new(_buffer, 0, _length);
        }

        public Span<T> WritableContent
        {
            get => new(_buffer, 0, _length);
        }

        public void Clear()
        {
            _length = 0;
        }

        private void EnsureSize(int size)
        {
            if (_buffer is null)
            {
                _buffer = new T[Math.Max(100, size)];
            }
            if (_length + size > _buffer.Length)
            {
                var newLength = Math.Max(_buffer.Length * 2, _length + size + 10);
                var newBuffer = new T[newLength];
                Buffer.BlockCopy(_buffer, 0, newBuffer, 0, 2 * _length);
                _buffer = newBuffer;
            }
        }

        public void Write(T c)
        {
            EnsureSize(1);
            _buffer[_length++] = c;
        }

        public void Write(ReadOnlySpan<T> span)
        {
            EnsureSize(span.Length);
            var dest = new Span<T>(_buffer, _length, span.Length);
            span.CopyTo(dest);
            _length += span.Length;
        }

        public void RemoveAt(int index)
        {
            Buffer.BlockCopy(_buffer, _size * (index + 1), _buffer, _size * index, _size * (_length - index - 1));
            _length -= 1;
        }

        public void RemoveRange(int index, int count)
        {
            Buffer.BlockCopy(_buffer, _size * (index + count), _buffer, _size * index, _size * (_length - index - count));
            _length -= count;
        }
    }
}
