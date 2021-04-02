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
        public double[] NumberStack;
        public object[] ObjectStack;
    }

    internal unsafe ref struct StackFrame
    {
        //In order for the linked list to work. This also stores index of StackSegment.
        public nint Head;
        public Span<nint> Last; //Last StackFrame's Head field.
        public Span<double> NumberFrame;
        public Span<object> ObjectFrame;
        public int NumberFrameStartIndex;
        public int ObjectFrameStartIndex;

        public StackMetaData MetaData;
        public bool OnSameSegment;

        private static ref nint GetLastImpl(ref nint x) => ref x;
        public static readonly delegate*<ref nint, ref StackFrame> GetLast =
            (delegate*<ref nint, ref StackFrame>)(delegate*<ref nint, ref nint>)&GetLastImpl;

        //Unspecialized type access (U).

        public VMSpecializationType GetTypeU(int u)
        {
            var d = BitConverter.DoubleToInt64Bits(NumberFrame[u]);
            if ((d & TypedValue.NNMaskL) == TypedValue.NNMarkL)
            {
                return (VMSpecializationType)((d >> 32) & 0xFF);
            }
            return VMSpecializationType.Double;
        }

        public int GetIntU(int u)
        {
            return (int)(uint)((ulong)BitConverter.DoubleToInt64Bits(NumberFrame[u]) & 0xFFFFFFFF);
        }

        public double GetDoubleU(int u)
        {
            return NumberFrame[u];
        }

        public string GetStringU(int u)
        {
            return (string)ObjectFrame[u];
        }

        public bool ToBoolU(int u)
        {
            var d = BitConverter.DoubleToInt64Bits(NumberFrame[u]);
            return (d >> 32) switch
            {
                (TypedValue.NNMark | (int)VMSpecializationType.Nil) => false,
                (TypedValue.NNMark | (int)VMSpecializationType.Bool) => (d & 0x1) != 0,
                _ => true,
            };
        }

        public TypedValue GetUpvalOU(int o, int index)
        {
            return ((TypedValue[])ObjectFrame[o])[index];
        }

        public void SetUpvalOU(int o, int index, TypedValue uval)
        {
            ((TypedValue[])ObjectFrame[o])[index] = uval;
        }

        public TypedValue GetU(int u)
        {
            return new TypedValue
            {
                Number = NumberFrame[u],
                Object = ObjectFrame[u],
            };
        }

        public void SetU(int u, TypedValue val)
        {
            NumberFrame[u] = val.Number;
            ObjectFrame[u] = val.Object;
        }

        public void CopyUU(int ufrom, int uto)
        {
            NumberFrame[uto] = NumberFrame[ufrom];
            ObjectFrame[uto] = ObjectFrame[ufrom];
        }

        public void ClearSigBlock()
        {
            MetaData.SigVOLength = MetaData.SigVVLength = 0;
            MetaData.SigDesc = SignatureDesc.Empty;
        }

        public void SetSigBlock(ref SignatureDesc desc, int o, int v)
        {
            MetaData.SigObjectOffset = o;
            MetaData.SigNumberOffset = v;
            MetaData.SigVOLength = 0;
            MetaData.SigVVLength = 0;
            MetaData.SigDesc = desc;
        }

        //Used by callee to add return values. This list will be checked and adjusted
        //again using TryAdjustSigBlockRight.
        public void ReplaceSigBlock(ref StackMetaData meta)
        {
            MetaData.SigDesc = meta.SigDesc;
            MetaData.SigVVLength = meta.SigVVLength;
            MetaData.SigVOLength = meta.SigVOLength;
        }

        //Adjust sig block. This operation handles sig block generated inside the same function
        //so it should never fail (or it's a program error), and we don't really need to check.
        public void ResizeSigBlockLeft(ref SignatureDesc desc, int o, int v)
        {
            if (MetaData.SigDesc.SigTypeId == 0)
            {
                //New sig block at given place.
                //Note that the (o,v) is the last item (inclusive) in the block.
                MetaData.SigObjectOffset = o - desc.SigFOLength;
                MetaData.SigNumberOffset = v - desc.SigFVLength;
                MetaData.SigDesc = desc;
            }
            else
            {
                Debug.Assert(desc.SigFVLength >= MetaData.SigDesc.SigFVLength);
                Debug.Assert(desc.SigFOLength >= MetaData.SigDesc.SigFOLength);

                //Extend to left.
                MetaData.SigNumberOffset -= desc.SigFVLength - MetaData.SigDesc.SigFVLength;
                MetaData.SigObjectOffset -= desc.SigFOLength - MetaData.SigDesc.SigFOLength;
                MetaData.SigDesc = desc;
            }
            //Don't clear vv and vo length.
        }

        //Adjust the sig block with given type without changing its starting position.
        //If varargStorage is not null, copy the vararg part to the separate list.
        public bool TryAdjustSigBlockRight(ref SignatureDesc desc, List<TypedValue> varargStorage, out int varargCount)
        {
            if (desc.SigTypeId == MetaData.SigDesc.SigTypeId)
            {
                //Same type.
                WriteVararg(varargStorage, out varargCount);
                return true;
            }
            if (!MetaData.SigDesc.SigType.IsCompatibleWith(desc.SigTypeId))
            {
                //Not compatible.
                if (!desc.SigType.IsUnspecialized)
                {
                    //Target is not unspecialized. We can do nothing here.
                    varargCount = 0;
                    return false;
                }

                //Target is unspecialized. We can make them compatible.
                Debug.Assert(!MetaData.SigDesc.SigType.IsUnspecialized);
                MetaData.SigDesc.SigType.AdjustStackToUnspecialized();
            }

            var diffO = desc.SigFOLength - MetaData.SigDesc.SigFOLength;
            var diffV = desc.SigFVLength - MetaData.SigDesc.SigFVLength;

            //Fill nils if necessary.
            if (diffO > 0)
            {
                ObjectFrame.Slice(MetaData.SigObjectOffset + MetaData.SigTotalOLength, diffO).Clear();
            }
            if (diffV > 0)
            {
                NumberFrame.Slice(MetaData.SigNumberOffset + MetaData.SigTotalVLength, diffV).Fill(TypedValue.Nil.Number);
            }
            MetaData.SigDesc = desc;
            MetaData.SigVOLength -= diffO; //Update variant part length for WriteVararg to work properly.
            MetaData.SigVVLength -= diffV;
            WriteVararg(varargStorage, out varargCount);
            return true;
        }

        private void WriteVararg(List<TypedValue> storage, out int count)
        {
            if (storage is null || !MetaData.SigDesc.SigType.Vararg.HasValue)
            {
                count = 0;
                return;
            }
            var vo = MetaData.SigDesc.HasVO;
            var vv = MetaData.SigDesc.HasVV;
            int start;
            if (vv && vo)
            {
                //Unspecialized vararg.
                Debug.Assert(MetaData.SigNumberOffset == MetaData.SigObjectOffset);
                Debug.Assert(MetaData.SigDesc.SigFVLength == MetaData.SigDesc.SigFOLength);
                Debug.Assert(MetaData.SigVOLength == MetaData.SigVVLength);
                start = MetaData.SigNumberOffset + MetaData.SigDesc.SigFVLength;
                count = MetaData.SigVVLength;
            }
            else if (vo)
            {
                start = MetaData.SigObjectOffset + MetaData.SigDesc.SigFOLength;
                count = MetaData.SigVOLength;
            }
            else
            {
                Debug.Assert(vv);
                start = MetaData.SigNumberOffset + MetaData.SigDesc.SigFVLength;
                count = MetaData.SigVVLength;
            }
            for (int i = 0; i < count; ++i)
            {
                storage.Add(new TypedValue
                {
                    Number = NumberFrame[start + i],
                    Object = null,
                });
            }
        }

        public void AppendVarargSig(VMSpecializationType type, int count)
        {
            //There can only be one vararg block.
            Debug.Assert(MetaData.SigVOLength == 0 && MetaData.SigVVLength == 0);
            MetaData.SigDesc = MetaData.SigDesc.WithVararg(type);
            var (v, o) = type.GetStorageType();
            if (v) MetaData.SigVVLength = count;
            if (o) MetaData.SigVOLength = count;
        }
    }

    internal struct SignatureDesc
    {
        public ulong SigTypeId;
        public StackSignature SigType;
        public int SigFOLength, SigFVLength; //Length of fixed part.
        public bool HasVO, HasVV;

        public static readonly SignatureDesc Empty = StackSignature.Empty.GetDesc();

        public SignatureDesc WithVararg(VMSpecializationType type)
        {
            if (HasVV || HasVO)
            {
                throw new InvalidOperationException();
            }
            var sig = SigType.WithVararg(type);
            var (vv, vo) = type.GetStorageType();
            return new SignatureDesc
            {
                SigTypeId = sig.GlobalId,
                SigType = sig,
                SigFOLength = SigFOLength,
                SigFVLength = SigFVLength,
                HasVO = vo,
                HasVV = vv,
            };
        }
    }

    //This struct stores other per-frame data (in addition to stack).
    internal struct StackMetaData
    {
        public Proto Func;

        //Signature region.

        public SignatureDesc SigDesc;

        public int SigNumberOffset; //Start of sig block.
        public int SigObjectOffset;
        public int SigVOLength; //Length of vararg part.
        public int SigVVLength;

        public int SigTotalOLength => SigDesc.SigFOLength + SigVOLength;
        public int SigTotalVLength => SigDesc.SigFVLength + SigVVLength;

        //Storage of the function vararg list (in VM's state.VarargStack).

        public int VarargStart;
        public int VarargLength;
        public VMSpecializationType VarargType;
    }

    internal struct SerializedStackFrame
    {
        public int Head;
        public ArraySegment<double> NumberFrame;
        public ArraySegment<object> ObjectFrame;
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
                NumberStack = new double[Options.StackSegmentSize],
                ObjectStack = new object[Options.StackSegmentSize],
            });
        }

        public StackFrame Allocate(int numSize, int objSize)
        {
            Debug.Assert(numSize < Options.MaxSingleFunctionStackSize);
            Debug.Assert(objSize < Options.MaxSingleFunctionStackSize);
            var s = _segments[0];
            return new StackFrame
            {
                Head = 0,
                Last = default,
                NumberFrame = s.NumberStack.AsSpan()[0..numSize],
                ObjectFrame = s.ObjectStack.AsSpan()[0..objSize],
            };
        }

        public StackFrame Allocate(ref StackFrame lastFrame, int numSize, int objSize, bool onSameSeg)
        {
            Debug.Assert(numSize < Options.MaxSingleFunctionStackSize);
            Debug.Assert(objSize < Options.MaxSingleFunctionStackSize);
            //Debug.Assert(_currentHead == (int)lastFrame.Head);

            var s = _segments[(int)lastFrame.Head];

            var newNStart = lastFrame.NumberFrameStartIndex + lastFrame.MetaData.SigNumberOffset;
            var newOStart = lastFrame.ObjectFrameStartIndex + lastFrame.MetaData.SigObjectOffset;
            var newNSize = lastFrame.MetaData.SigTotalVLength + numSize;
            var newOSize = lastFrame.MetaData.SigTotalOLength + objSize;

            var newHead = lastFrame.Head;
            
            if (s.NumberStack.Length < newNStart + numSize ||
                s.ObjectStack.Length < newOStart + objSize)
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
                    int numOffsetOnOldSeg = lastFrame.NumberFrameStartIndex;
                    int objOffsetOnOldSeg = lastFrame.ObjectFrameStartIndex;
                    if (lastFrame.OnSameSegment)
                    {
                        while (true)
                        {
                            unsafe
                            {
                                ref var f = ref StackFrame.GetLast(ref iter[0]);
                                Debug.Assert((int)f.Head == newHead);
                                iter = f.Last;
                                if (!f.OnSameSegment || iter.Length == 0)
                                {
                                    numOffsetOnOldSeg = f.NumberFrameStartIndex;
                                    objOffsetOnOldSeg = f.ObjectFrameStartIndex;
                                    break;
                                }
                            }
                        }
                    }
                    newNStart -= numOffsetOnOldSeg;
                    newOStart -= objOffsetOnOldSeg;
                    var totalNewNumSize = newNStart + newNSize + Options.MaxSingleFunctionStackSize;
                    var totalNewObjSize = newOStart + newOSize + Options.MaxSingleFunctionStackSize;
                    totalNewNumSize = Math.Max(totalNewNumSize, Options.StackSegmentSize);
                    totalNewObjSize = Math.Max(totalNewNumSize, Options.StackSegmentSize);

                    //Allocate new segment.
                    var newSegment = new StackSegment
                    {
                        NumberStack = new double[totalNewNumSize],
                        ObjectStack = new object[totalNewObjSize],
                    };
                    _segments[(int)newHead] = newSegment;
                    s.NumberStack.CopyTo(newSegment.NumberStack.AsSpan());
                    s.ObjectStack.CopyTo(newSegment.ObjectStack.AsSpan());

                    //Iterate and fix all frames.
                    iter = lastFrame.Last;
                    if (lastFrame.OnSameSegment)
                    {
                        while (iter.Length > 0)
                        {
                            unsafe
                            {
                                ref var f = ref StackFrame.GetLast(ref iter[0]);
                                f.ObjectFrameStartIndex -= objOffsetOnOldSeg;
                                f.NumberFrameStartIndex -= numOffsetOnOldSeg;
                                f.ObjectFrame = newSegment.ObjectStack.AsSpan()
                                    .Slice(f.ObjectFrameStartIndex, f.ObjectFrame.Length);
                                f.NumberFrame = newSegment.NumberStack.AsSpan()
                                    .Slice(f.NumberFrameStartIndex, f.NumberFrame.Length);

                                if (!f.OnSameSegment) break;
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
                            NumberStack = new double[Options.StackSegmentSize],
                            ObjectStack = new object[Options.StackSegmentSize],
                        });
                    }
                    s = _segments[(int)newHead];
                    var nframe = s.NumberStack.AsSpan()[0..newNSize];
                    var oframe = s.ObjectStack.AsSpan()[0..newOSize];

                    //Copy args from old frame.
                    var argNSize = lastFrame.MetaData.SigTotalVLength;
                    var argOSize = lastFrame.MetaData.SigTotalOLength;
                    lastFrame.NumberFrame.Slice(lastFrame.MetaData.SigNumberOffset, argNSize).CopyTo(nframe);
                    lastFrame.ObjectFrame.Slice(lastFrame.MetaData.SigObjectOffset, argOSize).CopyTo(oframe);
                    return new StackFrame
                    {
                        Head = newHead,
                        Last = MemoryMarshal.CreateSpan(ref lastFrame.Head, 1),
                        NumberFrame = nframe,
                        ObjectFrame = oframe,
                        OnSameSegment = onSameSeg,
                        MetaData =
                        {
                            SigNumberOffset = 0,
                            SigObjectOffset = 0,
                            SigVOLength = lastFrame.MetaData.SigVOLength,
                            SigVVLength = lastFrame.MetaData.SigVVLength,
                            SigDesc = lastFrame.MetaData.SigDesc,
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
                NumberFrameStartIndex = newNStart,
                ObjectFrameStartIndex = newOStart,
                NumberFrame = MemoryMarshal.CreateSpan(ref s.NumberStack[newNStart], newNSize),
                ObjectFrame = MemoryMarshal.CreateSpan(ref s.ObjectStack[newOStart], newOSize),
                OnSameSegment = onSameSeg,
                MetaData =
                {
                    SigNumberOffset = 0,
                    SigObjectOffset = 0,
                    SigVOLength = lastFrame.MetaData.SigVOLength,
                    SigVVLength = lastFrame.MetaData.SigVVLength,
                    SigDesc = lastFrame.MetaData.SigDesc,
                },
            };
        }

        private SerializedStackFrame SerializeFrameInternal(ref StackFrame frame)
        {
            var seg = _segments[(int)frame.Head];
            return new SerializedStackFrame
            {
                Head = (int)frame.Head,
                NumberFrame = new(seg.NumberStack, frame.NumberFrameStartIndex, frame.NumberFrame.Length),
                ObjectFrame = new(seg.ObjectStack, frame.ObjectFrameStartIndex, frame.ObjectFrame.Length),
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
                NumberFrame = s.NumberFrame.AsSpan(),
                ObjectFrame = s.ObjectFrame.AsSpan(),
                MetaData = s.MetaData,
            };
            return true;
        }
    }
}
