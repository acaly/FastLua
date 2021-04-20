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

        public ref TypedValue this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.Add(ref MemoryMarshal.GetReference(Span), index);
        }

        public ref TypedValue GetUpvalue(int stackOffset, int index)
        {
            return ref ((TypedValue[])this[stackOffset].Object)[index];
        }
    }

    public struct StackInfo
    {
        internal Thread Thread;
        internal int StackFrame;

        public void Write(int start, Span<TypedValue> values)
        {
            if (values.Length != 0)
            {
                //TODO write as polymorphic values
                throw new NotImplementedException();
            }

            Thread.ClearSigBlock();
            var empty = SignatureDesc.Empty;
            Thread.SetSigBlock(ref empty, start);
        }

        //TODO read
        //
    }

    internal struct SignatureDesc
    {
        public ulong SigTypeId;
        public StackSignature SigType;
        public int SigFLength; //Length of fixed part.
        public bool HasV;

        public static readonly SignatureDesc Null = StackSignature.Null.GetDesc();
        public static readonly SignatureDesc Empty = StackSignature.Empty.GetDesc();

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
    internal struct StackMetaData
    {
        //Storage of the function vararg list (in VM's state.VarargStack).

        public int VarargStart;
        public int VarargLength;
        public VMSpecializationType VarargType;

    }

    internal struct StackFrame
    {
        public int Segment;
        public int Offset;
        public int Length;
        public int PC;
        public bool ForceOnSameSegment;

        public StackMetaData MetaData;
    }

    internal sealed class StackManager
    {
        private readonly List<TypedValue[]> _segments = new();
        private FastList<StackFrame> _serializedFrames;

        public StackManager()
        {
            _segments.Add(new TypedValue[Options.StackSegmentSize]);
            _serializedFrames.Init(Options.InitialFrameCapacity);
        }

        public StackFrameValues GetValues(ref StackFrame frame)
        {
            return new()
            {
                Span = _segments[frame.Segment].AsSpan().Slice(frame.Offset, frame.Length),
            };
        }

        public ref StackFrame Get(int index)
        {
            return ref _serializedFrames[index];
        }

        public ref StackFrame AllocateFirst(int size)
        {
            Debug.Assert(_serializedFrames.Count == 0);
            Debug.Assert(size < Options.MaxSingleFunctionStackSize);

            ref var ret = ref _serializedFrames.Add();
            ret.Segment = 0;
            ret.Offset = 0;
            ret.Length = size;
            ret.MetaData = default;

            return ref ret;
        }

        public ref StackFrame AllocateNext(Thread thread, ref StackFrame lastFrame,
            int size, ref bool onSameSeg)
        {
            Debug.Assert(_serializedFrames.Count > 0);
            Debug.Assert(Unsafe.AreSame(ref lastFrame, ref _serializedFrames.Last));
            Debug.Assert(size < Options.MaxSingleFunctionStackSize);

            ref var ret = ref _serializedFrames.Add();

            var newHead = lastFrame.Segment;
            var newStart = lastFrame.Offset + thread.SigOffset;
            var newSize = thread.SigTotalLength + size;
            var s = _segments[newHead];
            //newSize can be larget than MaxSingleFunctionStackSize. Is it desired?

            if (s.Length >= newStart + newSize)
            {
                //Case 1: Enough space. Just grow.
                ret.Segment = newHead;
                ret.Offset = newStart;
                ret.Length = newSize;
                ret.ForceOnSameSegment = onSameSeg;

                onSameSeg = true;
                return ref ret;
            }

            if (!onSameSeg)
            {
                //Case 2: Allocate the new frame on the next segment.

                //Allocate a new segment if necessary.
                if (++newHead == _segments.Count)
                {
                    _segments.Add(new TypedValue[Options.StackSegmentSize]);
                }

                //Copy args from old frame.
                var argSize = thread.SigTotalLength;
                var lastFrameValues = _segments[lastFrame.Segment].AsSpan();
                lastFrameValues = lastFrameValues.Slice(lastFrame.Offset, lastFrame.Length);
                lastFrameValues = lastFrameValues.Slice(thread.SigOffset, argSize);
                lastFrameValues.CopyTo(_segments[newHead].AsSpan()[0..newSize]);

                ret.Segment = newHead;
                ret.Offset = 0;
                ret.Length = newSize;
                ret.ForceOnSameSegment = false;

                onSameSeg = false;
                return ref ret;
            }

            //Case 3: Lua stack relocation.
            //New frame needs to be on the same segment, but current segment does not have
            //enough space. Need to relocate all existing frames that requires OnSameSegment.
            //This should be very rare for any normal code.

            //Remove unused segments. We don't want to keep more than one large segment.
            _segments.RemoveRange(newHead + 1, _segments.Count - newHead - 1);

            //Find the range of frames to relocate.
            var relocationBegin = _serializedFrames.Count - 1;
            while (_serializedFrames[relocationBegin].ForceOnSameSegment)
            {
                Debug.Assert(relocationBegin > 0);
                Debug.Assert(_serializedFrames[relocationBegin - 1].Segment == newHead);
                relocationBegin -= 1;
            }

            //Calculate total size required for all relocated frames.
            var minNewSize = lastFrame.Offset - _serializedFrames[relocationBegin].Offset;
            minNewSize += thread.SigOffset + newSize;
            var realNewSize = Options.StackSegmentSize * 2;
            while (realNewSize < minNewSize)
            {
                realNewSize *= 2;
            }

            //Relocate.
            var newSegment = new TypedValue[realNewSize];
            var relocationOffset = _serializedFrames[relocationBegin].Offset;
            _segments[newHead].AsSpan()[relocationOffset..]
                .CopyTo(newSegment.AsSpan());
            _segments[newHead] = s = newSegment;
            for (int i = relocationBegin; i < _serializedFrames.Count; ++i)
            {
                ref var f = ref _serializedFrames[i];
                f.Offset -= relocationOffset;
            }
            newStart -= relocationOffset;

            //Confirm we have leave enough space.
            Debug.Assert(s.Length < newStart + newSize);

            //Use RecoverableException to restart all Lua frames. This is necessary
            //to update the Span<TypedValue> on native C# stack.
            //We can alternatively update all of them here, but that would require
            //maintaining a linked list of Spans, which introduces overhead for each
            //CALL/CALLC.
            throw new RecoverableException();
        }

        public void Deallocate(ref StackFrame frame)
        {
            Debug.Assert(Unsafe.AreSame(ref frame, ref _serializedFrames.Last));
            _serializedFrames.RemoveLast();
        }
    }
}
