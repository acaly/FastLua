using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    public sealed class Thread
    {
        internal StringBuilder StringBuilder = new();
        internal List<TypedValue> VarargStack = new();
        internal int VarargTotalLength = 0;

        internal SignatureDesc SigDesc;

        internal int SigOffset; //Start of sig block.
        internal int SigVLength; //Length of vararg part.
        internal int SigTotalLength => SigDesc.SigFLength + SigVLength;

        public Thread()
        {
            _segments.Add(new TypedValue[Options.StackSegmentSize]);
            _serializedFrames.Init(Options.InitialFrameCapacity);
        }

        //Signature region.

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ClearSigBlock()
        {
            SigVLength = 0;
            SigDesc = SignatureDesc.Null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetSigBlock(ref SignatureDesc desc, int pos)
        {
            //Keep VLength.
            SigOffset = pos;
            SigDesc = desc;
        }

        //Set the sig to contain only a vararg part.
        //This is always followed by an adjustment.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetSigBlockVararg(ref SignatureDesc desc, int pos, int length)
        {
            Debug.Assert(desc.SigFLength == 0);
            SigOffset = pos;
            SigDesc = desc;
            SigVLength = length;
        }

        //Adjust sig block. This operation handles sig block generated inside the same function
        //so it should never fail (or it's a program error), and we don't really need to check.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int ResizeSigBlockLeft(ref SignatureDesc desc, int pos)
        {
            if (SigDesc.SigTypeId == (ulong)WellKnownStackSignature.Null)
            {
                //New sig block at given place.
                SigDesc = desc;
                return SigOffset = pos;
            }
            else
            {
                Debug.Assert(desc.SigFLength >= SigDesc.SigFLength);

                //Extend to left.
                SigDesc = desc;
                return SigOffset -= desc.SigFLength - SigDesc.SigFLength;
            }
            //Don't clear v length.
        }

        //Adjust the sig block with given type without changing its starting position.
        //If varargStorage is not null, copy the vararg part to the separate list.
        internal bool TryAdjustSigBlockRight(ref StackFrameValues values, ref SignatureDesc desc)
        {
            if (desc.SigTypeId == SigDesc.SigTypeId)
            {
                //Same type.
                return true;
            }
            if (!SigDesc.SigType.IsCompatibleWith(desc.SigType))
            {
                //Not compatible.
                if (!desc.SigType.IsUnspecialized)
                {
                    //Target is not unspecialized. We can do nothing here.
                    return false;
                }

                //Target is unspecialized. We can make them compatible.
                Debug.Assert(!SigDesc.SigType.IsUnspecialized);
                SigDesc.SigType.AdjustStackToUnspecialized();
            }

            var diff = desc.SigFLength - SigDesc.SigFLength;

            //Fill nils if necessary.
            if (desc.SigFLength > SigDesc.SigFLength + SigVLength)
            {
                values.Span.Slice(SigOffset + SigTotalLength, diff).Fill(TypedValue.Nil);
                SigVLength = 0;
            }
            else
            {
                //Update variant part length for WriteVararg to work properly.
                SigVLength -= desc.SigFLength - SigDesc.SigFLength;
            }

            SigDesc = desc;

            return true;
        }

        internal void WriteVararg(ref StackFrameValues values, List<TypedValue> storage, ref StackFrameVarargInfo varargInfo)
        {
            if (!SigDesc.SigType.Vararg.HasValue)
            {
                DiscardVararg(ref values);
                return;
            }
            Debug.Assert(SigDesc.HasV);

            varargInfo.VarargStart = storage.Count;
            varargInfo.VarargLength = SigVLength;

            var start = SigOffset + SigDesc.SigFLength;
            for (int i = 0; i < varargInfo.VarargLength; ++i)
            {
                storage.Add(values[start + i]);
                values[start + i].Object = null;
            }
        }

        internal void DiscardVararg(ref StackFrameValues values)
        {
            if (SigVLength > 0)
            {
                var start = SigOffset + SigDesc.SigFLength;
                for (int i = 0; i < SigVLength; ++i)
                {
                    values[start + i].Object = null;
                }
            }
        }

        //Stack management.

        private readonly List<TypedValue[]> _segments = new();
        private FastList<StackFrame> _serializedFrames;

        public StackInfo AllocateCSharpStack(int size)
        {
            AllocateFirstFrame(size);
            return new StackInfo
            {
                Thread = this,
                StackFrame = 0,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal StackFrameValues GetFrameValues(ref StackFrame frame)
        {
            return new()
            {
                Span = new(_segments[frame.Segment], frame.Offset, frame.Length),
            };
        }

        internal ref StackFrame GetFrame(int index)
        {
            return ref _serializedFrames[index];
        }

        internal ref StackFrame AllocateFirstFrame(int size)
        {
            Debug.Assert(_serializedFrames.Count == 0);
            Debug.Assert(size < Options.MaxSingleFunctionStackSize);

            ref var ret = ref _serializedFrames.Add();
            ret.Segment = 0;
            ret.Offset = 0;
            ret.Length = size;
            ret.VarargInfo = default;

            return ref ret;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref StackFrame AllocateNextFrame(ref StackFrame lastFrame, int size, bool onSameSeg)
        {
            Debug.Assert(_serializedFrames.Count > 0);
            Debug.Assert(Unsafe.AreSame(ref lastFrame, ref _serializedFrames.Last));
            Debug.Assert(size < Options.MaxSingleFunctionStackSize);

            var newHead = lastFrame.Segment;
            var newStart = lastFrame.Offset + SigOffset;
            var newSize = SigTotalLength + size;
            var s = _segments[newHead];
            //newSize can be larget than MaxSingleFunctionStackSize. Is it desired?

            if (s.Length < newStart + newSize)
            {
                return ref AllocateNextFrameSlow(ref lastFrame, size, onSameSeg);
            }

            ref var ret = ref _serializedFrames.Add();

            //Enough space. Just grow.
            ret.Segment = newHead;
            ret.Offset = newStart;
            ret.Length = newSize;
            ret.ForceOnSameSegment = onSameSeg;

            return ref ret;
        }

        private ref StackFrame AllocateNextFrameSlow(ref StackFrame lastFrame, int size, bool onSameSeg)
        {
            ref var ret = ref _serializedFrames.Add();

            var newHead = lastFrame.Segment;
            var newStart = lastFrame.Offset + SigOffset;
            var newSize = SigTotalLength + size;
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
                var argSize = SigTotalLength;
                var lastFrameValues = _segments[lastFrame.Segment].AsSpan();
                lastFrameValues = lastFrameValues.Slice(lastFrame.Offset, lastFrame.Length);
                lastFrameValues = lastFrameValues.Slice(SigOffset, argSize);
                lastFrameValues.CopyTo(_segments[newHead].AsSpan()[0..newSize]);

                ret.Segment = newHead;
                ret.Offset = 0;
                ret.Length = newSize;
                ret.ForceOnSameSegment = false;

                return ref ret;
            }

            //Lua stack relocation.
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
            minNewSize += SigOffset + newSize;
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

        internal void DeallocateFrame(ref StackFrame frame)
        {
            Debug.Assert(Unsafe.AreSame(ref frame, ref _serializedFrames.Last));
            _serializedFrames.RemoveLast();
        }
    }
}
