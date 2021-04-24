using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    public sealed class Thread
    {
        internal StringBuilder StringBuilder = new();
        internal List<TypedValue> VarargStack = new();
        internal int VarargTotalLength = 0;

        public Thread()
        {
            _segments.Add(new TypedValue[Options.StackSegmentSize]);
            _serializedFrames.Init(Options.InitialFrameCapacity);
        }

        //Stack management.

        private readonly List<TypedValue[]> _segments = new();
        private FastList<StackFrame> _serializedFrames;

        internal Stack<LClosure> ClosureStack = new();

        public AsyncStackInfo AllocateRootCSharpStack(int size)
        {
            AllocateFirstFrame(size);
            return new AsyncStackInfo(this, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal StackFrameValues GetFrameValues(ref StackFrame frame)
        {
            return new(new(_segments[frame.Segment], frame.Offset, frame.Length));
        }

        internal ref StackFrame GetFrame(int index)
        {
            return ref _serializedFrames[index];
        }

        internal ref StackFrame AllocateFirstFrame(int size)
        {
            Debug.Assert(_serializedFrames.Count == 0);
            Debug.Assert(size < Options.MaxSingleFunctionStackSize);
            Debug.Assert(Options.MaxSingleFunctionStackSize < ushort.MaxValue);

            ref var ret = ref _serializedFrames.Add();
            ret.Segment = 0;
            ret.Offset = 0;
            ret.Length = size;
            ret.VarargInfo = default;

            return ref ret;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Span<StackFrame> AllocateNextFrame(ref StackFrame lastFrame, int offset, int totalLength,
            int size, bool onSameSeg)
        {
            Debug.Assert(_serializedFrames.Count > 0);
            Debug.Assert(Unsafe.AreSame(ref lastFrame, ref _serializedFrames.Last));
            Debug.Assert(size < Options.MaxSingleFunctionStackSize);

            var newHead = lastFrame.Segment;
            var newStart = lastFrame.Offset + offset;
            var newSize = totalLength + size;
            var s = _segments[newHead];
            //newSize can be larget than MaxSingleFunctionStackSize. Is it desired?

            if (s.Length < newStart + newSize)
            {
                return AllocateNextFrameSlow(ref lastFrame, offset, totalLength, size, onSameSeg);
            }

            ref var ret = ref _serializedFrames.Add();

            //Enough space. Just grow.
            ret.Segment = newHead;
            ret.Offset = newStart;
            ret.Length = newSize;
            ret.ForceOnSameSegment = onSameSeg;
            ret.ActualOnSameSegment = true;

            return MemoryMarshal.CreateSpan(ref ret, 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal AsyncStackInfo ConvertToNativeFrame(ref StackFrame frame)
        {
            Debug.Assert(Unsafe.AreSame(ref frame, ref _serializedFrames.Last));
            return new(this, _serializedFrames.Count - 1);
        }

        private Span<StackFrame> AllocateNextFrameSlow(ref StackFrame lastFrame, int offset, int totalLength,
            int size, bool onSameSeg)
        {
            ref var ret = ref _serializedFrames.Add();

            var newHead = lastFrame.Segment;
            var newStart = lastFrame.Offset + offset;
            var newSize = totalLength + size;
            var s = _segments[newHead];

            if (!onSameSeg)
            {
                //Allocate the new frame on the next segment.

                //Allocate a new segment if necessary.
                if (++newHead == _segments.Count)
                {
                    _segments.Add(new TypedValue[Options.StackSegmentSize]);
                }

                //Copy args from old frame.
                var argSize = totalLength;
                var lastFrameValues = _segments[lastFrame.Segment].AsSpan();
                lastFrameValues = lastFrameValues.Slice(lastFrame.Offset, lastFrame.Length);
                lastFrameValues = lastFrameValues.Slice(offset, argSize);
                lastFrameValues.CopyTo(_segments[newHead].AsSpan()[0..newSize]);

                ret.Segment = newHead;
                ret.Offset = 0;
                ret.Length = newSize;
                ret.ForceOnSameSegment = false;
                ret.ActualOnSameSegment = false;

                return MemoryMarshal.CreateSpan(ref ret, 1);
            }

            //Lua stack relocation.
            //New frame needs to be on the same segment, but current segment does not have
            //enough space. Need to relocate all existing frames that requires OnSameSegment.
            //This should be very rare for any normal code.

            ReallocateFrameInternal(ref lastFrame, offset + newSize);

            //Use RecoverableException to restart all Lua frames. This is necessary
            //to update the Span<TypedValue> on native C# stack.
            //We can alternatively update all of them here, but that would require
            //maintaining a linked list of Spans, which introduces overhead for each
            //CALL/CALLC.
            throw new RecoverableException();
        }

        internal void ReallocateFrameInternal(ref StackFrame currentFrame, int currentFrameNewSize)
        {
            Debug.Assert(Unsafe.AreSame(ref currentFrame, ref _serializedFrames.Last));
            if (currentFrame.Length >= currentFrameNewSize)
            {
                return;
            }

            var seg = currentFrame.Segment;

            //Remove unused segments. We don't want to keep more than one large segment.
            _segments.RemoveRange(seg + 1, _segments.Count - seg - 1);

            //Find the range of frames to relocate.
            var relocationBegin = _serializedFrames.Count - 1;
            while (_serializedFrames[relocationBegin].ForceOnSameSegment)
            {
                Debug.Assert(relocationBegin > 0);
                Debug.Assert(_serializedFrames[relocationBegin - 1].Segment == seg);
                relocationBegin -= 1;
            }

            //Calculate total size required for all relocated frames.
            var minNewSize = currentFrame.Offset - _serializedFrames[relocationBegin].Offset;
            minNewSize += currentFrameNewSize;
            var realNewSize = Options.StackSegmentSize * 2;
            while (realNewSize < minNewSize)
            {
                realNewSize *= 2;
            }
            if (realNewSize > ushort.MaxValue)
            {
                //Lua stack overflow.
                throw new Exception();
            }

            //Relocate.
            var newSegment = new TypedValue[realNewSize];
            var relocationOffset = _serializedFrames[relocationBegin].Offset;
            _segments[seg].AsSpan()[relocationOffset..]
                .CopyTo(newSegment.AsSpan());
            _segments[seg] = newSegment;
            for (int i = relocationBegin; i < _serializedFrames.Count; ++i)
            {
                ref var f = ref _serializedFrames[i];
                f.Offset -= relocationOffset;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void DeallocateFrame(ref Span<StackFrame> frame)
        {
            Debug.Assert(Unsafe.AreSame(ref frame[0], ref _serializedFrames.Last));
            _serializedFrames.RemoveLast();
            if (_serializedFrames.Count > 0)
            {
                frame = MemoryMarshal.CreateSpan(ref _serializedFrames.Last, 1);
            }
            else
            {
                frame = default;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref StackFrame GetLastFrame(ref StackFrame frame)
        {
            return ref Unsafe.Add(ref frame, -1);
        }
    }
}
