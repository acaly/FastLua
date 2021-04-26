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
            _firstUnusedFrameRef = PrepareFrames();
        }

        //Stack management.

        private readonly List<TypedValue[]> _segments = new();
        private readonly UnsafeStorage<StackFrame> _serializedFrameStorage = new(Options.InitialFrameCapacity);
        private StackFrameRef _lastInitializedFrameRef;
        private StackFrameRef _firstUnusedFrameRef;

        internal Stack<LClosure> ClosureStack = new();

        private unsafe StackFrameRef PrepareFrames()
        {
            var last = _lastInitializedFrameRef;
            StackFrameRef ret = default;
            for (int i = 0; i < Options.InitialFrameCapacity; ++i)
            {
                var next = new StackFrameRef(_serializedFrameStorage.Get());
                next.Data = default;
                next.Data.Prev = last;
                if (!last.IsNull)
                {
                    last.Data.Next = next;
                    next.Data.Index = last.Data.Index + 1;
                }
                if (i == 0)
                {
                    ret = next;
                }
                last = next;
            }
            _lastInitializedFrameRef = last;
            return ret;
        }

        public AsyncStackInfo AllocateRootCSharpStack(int size)
        {
            return new AsyncStackInfo(this, AllocateFirstFrame(size));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Span<TypedValue> GetFrameValues(in StackFrame frame)
        {
            return new(_segments[frame.Segment], frame.Offset, frame.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private StackFrameRef AllocateInternal()
        {
            var ret = _firstUnusedFrameRef;
            if ((_firstUnusedFrameRef = ret.Data.Next).IsNull)
            {
                _firstUnusedFrameRef = ret.Data.Next = PrepareFrames();
            }
            return ret;
        }

        internal StackFrameRef AllocateFirstFrame(int size)
        {
            //TODO maybe we still need an assertion here
            //Debug.Assert(_serializedFrames.Count == 0);
            Debug.Assert(size < Options.MaxSingleFunctionStackSize);
            Debug.Assert(Options.MaxSingleFunctionStackSize < ushort.MaxValue);

            var ret = AllocateInternal();
            ret.Data.Segment = 0;
            ret.Data.Offset = 0;
            ret.Data.Length = size;
            ret.Data.VarargInfo = default;

            return ret;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal StackFrameRef AllocateNextFrame(StackFrameRef lastFrame, int offset, int totalLength,
            int size, bool onSameSeg)
        {
            //TODO maybe we still need an assertion here
            //Debug.Assert(_serializedFrames.Count > 0);
            Debug.Assert(lastFrame.Data.Next == _firstUnusedFrameRef);
            Debug.Assert(size < Options.MaxSingleFunctionStackSize);

            var newHead = lastFrame.Data.Segment;
            var newStart = lastFrame.Data.Offset + offset;
            var newSize = totalLength + size;
            var s = _segments[newHead];
            //newSize can be larget than MaxSingleFunctionStackSize. Is it desired?

            if (s.Length < newStart + newSize)
            {
                return AllocateNextFrameSlow(lastFrame, offset, totalLength, size, onSameSeg);
            }

            var ret = AllocateInternal();

            //Enough space. Just grow.
            ret.Data.Segment = newHead;
            ret.Data.Offset = newStart;
            ret.Data.Length = newSize;
            ret.Data.ForceOnSameSegment = onSameSeg;
            ret.Data.ActualOnSameSegment = true;

            return ret;
        }

        private StackFrameRef AllocateNextFrameSlow(StackFrameRef lastFrame, int offset, int totalLength,
            int size, bool onSameSeg)
        {
            var ret = AllocateInternal();

            var newHead = lastFrame.Data.Segment;
            var newStart = lastFrame.Data.Offset + offset;
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
                var lastFrameValues = _segments[lastFrame.Data.Segment].AsSpan();
                lastFrameValues = lastFrameValues.Slice(lastFrame.Data.Offset, lastFrame.Data.Length);
                lastFrameValues = lastFrameValues.Slice(offset, argSize);
                lastFrameValues.CopyTo(_segments[newHead].AsSpan()[0..newSize]);

                ret.Data.Segment = newHead;
                ret.Data.Offset = 0;
                ret.Data.Length = newSize;
                ret.Data.ForceOnSameSegment = false;
                ret.Data.ActualOnSameSegment = false;

                return ret;
            }

            //Lua stack relocation.
            //New frame needs to be on the same segment, but current segment does not have
            //enough space. Need to relocate all existing frames that requires OnSameSegment.
            //This should be very rare for any normal code.

            ReallocateFrameInternal(lastFrame, offset + newSize);

            //Use RecoverableException to restart all Lua frames. This is necessary
            //to update the Span<TypedValue> on native C# stack.
            //We can alternatively update all of them here, but that would require
            //maintaining a linked list of Spans, which introduces overhead for each
            //CALL/CALLC.
            throw new RecoverableException();
        }

        internal void ReallocateFrameInternal(StackFrameRef currentFrame, int currentFrameNewSize)
        {
            Debug.Assert(currentFrame.Data.Next == _firstUnusedFrameRef);
            if (currentFrame.Data.Length >= currentFrameNewSize)
            {
                return;
            }

            var seg = currentFrame.Data.Segment;

            //Remove unused segments. We don't want to keep more than one large segment.
            _segments.RemoveRange(seg + 1, _segments.Count - seg - 1);

            //Find the range of frames to relocate.
            var relocationBegin = currentFrame;
            while (currentFrame.Data.ForceOnSameSegment)
            {
                Debug.Assert(!relocationBegin.Data.Prev.IsNull);
                Debug.Assert(relocationBegin.Data.Prev.Data.Segment == seg);
                relocationBegin = relocationBegin.Data.Prev;
            }

            //Calculate total size required for all relocated frames.
            var minNewSize = currentFrame.Data.Offset - relocationBegin.Data.Offset;
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
            var relocationOffset = relocationBegin.Data.Offset;
            _segments[seg].AsSpan()[relocationOffset..]
                .CopyTo(newSegment.AsSpan());
            _segments[seg] = newSegment;
            for (var i = relocationBegin; i != currentFrame; i = i.Data.Next)
            {
                i.Data.Offset -= relocationOffset;
            }
        }

        internal void DeallocateFrame(ref StackFrameRef frame)
        {
            Debug.Assert(frame.Data.Next == _firstUnusedFrameRef);
            _firstUnusedFrameRef = frame;
            frame = frame.Data.Prev;
        }
    }
}
