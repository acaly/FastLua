using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace FastLua.VM
{
    internal sealed class StackSegment
    {
        public TypedValue[] ValueStack;
    }

    internal unsafe ref struct StackFrame
    {
        //In order for the linked list to work. This also stores index of StackSegment.
        public nint Head;
        public Span<nint> Last; //Last StackFrame's Head field.
        public Span<TypedValue> ValueFrame;
        public int ValueFrameStartIndex;

        public StackMetaData MetaData;

        private static ref nint GetLastImpl(ref nint x) => ref x;
        public static readonly delegate*<ref nint, ref StackFrame> GetLast =
            (delegate*<ref nint, ref StackFrame>)(delegate*<ref nint, ref nint>)&GetLastImpl;

        //Unspecialized type access (U).

        public VMSpecializationType GetTypeU(int u)
        {
            var d = BitConverter.DoubleToInt64Bits(ValueFrame[u].Number);
            if ((d & TypedValue.NNMaskL) == TypedValue.NNMarkL)
            {
                return (VMSpecializationType)((d >> 32) & 0xFF);
            }
            return VMSpecializationType.Double;
        }

        public int GetIntU(int u)
        {
            return (int)(uint)((ulong)BitConverter.DoubleToInt64Bits(ValueFrame[u].Number) & 0xFFFFFFFF);
        }

        public double GetDoubleU(int u)
        {
            return ValueFrame[u].Number;
        }

        public string GetStringU(int u)
        {
            return (string)ValueFrame[u].Object;
        }

        public bool ToBoolU(int u)
        {
            var d = BitConverter.DoubleToInt64Bits(ValueFrame[u].Number);
            return (d >> 32) switch
            {
                (TypedValue.NNMark | (int)VMSpecializationType.Nil) => false,
                (TypedValue.NNMark | (int)VMSpecializationType.Bool) => (d & 0x1) != 0,
                _ => true,
            };
        }

        public TypedValue GetUpvalOU(int o, int index)
        {
            return ((TypedValue[])ValueFrame[o].Object)[index];
        }

        public void SetUpvalOU(int o, int index, TypedValue uval)
        {
            ((TypedValue[])ValueFrame[o].Object)[index] = uval;
        }
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

        public bool OnSameSegment;
    }

    internal struct SerializedStackFrame
    {
        public int Head;
        public ArraySegment<TypedValue> ValueFrame;
        public StackMetaData MetaData;
    }

    internal sealed class StackManager
    {
        private readonly List<StackSegment> _segments = new();
        private readonly Stack<SerializedStackFrame> _serializedFrames = new();

        public StackManager()
        {
            _segments.Add(new()
            {
                ValueStack = new TypedValue[Options.StackSegmentSize],
            });
        }

        public StackFrame Allocate(int size)
        {
            Debug.Assert(size < Options.MaxSingleFunctionStackSize);
            var s = _segments[0];
            return new StackFrame
            {
                Head = 0,
                Last = default,
                ValueFrame = s.ValueStack.AsSpan()[0..size],
            };
        }

        public StackFrame Allocate(Thread thread, ref StackFrame lastFrame, int size, bool onSameSeg)
        {
            Debug.Assert(size < Options.MaxSingleFunctionStackSize);

            var s = _segments[(int)lastFrame.Head];

            var newStart = lastFrame.ValueFrameStartIndex + thread.SigOffset;
            var newSize = thread.SigTotalLength + size;

            var newHead = lastFrame.Head;
            
            if (s.ValueStack.Length < newStart + newSize)
            {
                if (onSameSeg)
                {
                    //The most difficult path: new frame needs to be on the same segment, but current
                    //segment does not have enough space. Need to relocate all existing frames
                    //that requires OnSameSegment.
                    //This should be very rare for any normal code.

                    //Calculate total size required for all frames.
                    //We assume that each frame actually only requires the space up to its sig offset.
                    //This is ensured by the current calling convension.
                    Span<nint> iter = lastFrame.Last;
                    int offsetOnOldSeg = lastFrame.ValueFrameStartIndex;
                    if (lastFrame.MetaData.OnSameSegment)
                    {
                        while (true)
                        {
                            unsafe
                            {
                                ref var f = ref StackFrame.GetLast(ref iter[0]);
                                Debug.Assert((int)f.Head == newHead);
                                iter = f.Last;
                                if (!f.MetaData.OnSameSegment || iter.Length == 0)
                                {
                                    offsetOnOldSeg = f.ValueFrameStartIndex;
                                    break;
                                }
                            }
                        }
                    }
                    newStart -= offsetOnOldSeg;
                    var totalNewSize = newStart + newSize + Options.MaxSingleFunctionStackSize;
                    totalNewSize = Math.Max(totalNewSize, Options.StackSegmentSize);

                    //Allocate new segment.
                    var newSegment = new StackSegment
                    {
                        ValueStack = new TypedValue[totalNewSize],
                    };
                    _segments[(int)newHead] = newSegment;
                    s.ValueStack.CopyTo(newSegment.ValueStack.AsSpan());

                    //Iterate and fix all frames.
                    iter = lastFrame.Last;
                    if (lastFrame.MetaData.OnSameSegment)
                    {
                        while (iter.Length > 0)
                        {
                            unsafe
                            {
                                ref var f = ref StackFrame.GetLast(ref iter[0]);
                                f.ValueFrameStartIndex -= offsetOnOldSeg;
                                f.ValueFrame = newSegment.ValueStack.AsSpan()
                                    .Slice(f.ValueFrameStartIndex, f.ValueFrame.Length);

                                if (!f.MetaData.OnSameSegment) break;
                                iter = f.Last;
                            }
                        }
                    }

                    s = newSegment;
                    //Fall through and allocate the new frame.
                }
                else
                {
                    //Allocate new frame on a new segment.
                    if (++newHead == _segments.Count)
                    {
                        _segments.Add(new()
                        {
                            ValueStack = new TypedValue[Options.StackSegmentSize],
                        });
                    }
                    s = _segments[(int)newHead];
                    var frame = s.ValueStack.AsSpan()[0..newSize];

                    //Copy args from old frame.
                    var argSize = thread.SigTotalLength;
                    lastFrame.ValueFrame.Slice(thread.SigOffset, argSize).CopyTo(frame);
                    return new StackFrame
                    {
                        Head = newHead,
                        Last = MemoryMarshal.CreateSpan(ref lastFrame.Head, 1),
                        ValueFrame = frame,
                        MetaData =
                        {
                            OnSameSegment = onSameSeg,
                        },
                    };
                }
            }

            //Enough space. Just grow.
            //Don't need to copy args. Use extend to allocate frame after the current one.
            //In order to reduce overhead on bounds check, we can remove the manual check above
            //and instead catch out-of-range exception from AsSpan().Slice().
            return new StackFrame
            {
                Head = newHead,
                Last = MemoryMarshal.CreateSpan(ref lastFrame.Head, 1),
                ValueFrameStartIndex = newStart,
                ValueFrame = MemoryMarshal.CreateSpan(ref s.ValueStack[newStart], newSize),
                MetaData =
                {
                    OnSameSegment = onSameSeg,
                },
            };
        }

        private SerializedStackFrame SerializeFrameInternal(ref StackFrame frame)
        {
            var seg = _segments[(int)frame.Head];
            return new SerializedStackFrame
            {
                Head = (int)frame.Head,
                ValueFrame = new(seg.ValueStack, frame.ValueFrameStartIndex, frame.ValueFrame.Length),
                MetaData = frame.MetaData,
            };
        }

        public void SerializeAll(ref StackFrame lastFrame)
        {
            if (_serializedFrames.Count != 0)
            {
                throw new InvalidOperationException();
            }

            Span<nint> next;
            do
            {
                _serializedFrames.Push(SerializeFrameInternal(ref lastFrame));
                next = lastFrame.Last;
            } while (next.Length > 0);
        }

        public bool DeserializeSingle(ref StackFrame lastFrame, out StackFrame frame)
        {
            if (!_serializedFrames.TryPop(out var s))
            {
                frame = default;
                return false;
            }
            frame = new StackFrame()
            {
                Head = s.Head,
                Last = MemoryMarshal.CreateSpan(ref lastFrame.Head, 1),
                ValueFrame = s.ValueFrame.AsSpan(),
                ValueFrameStartIndex = s.ValueFrame.Offset,
                MetaData = s.MetaData,
            };
            return true;
        }
    }
}
