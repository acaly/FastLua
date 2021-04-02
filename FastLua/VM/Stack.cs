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
        public StackFrameList<double> NumberStack;
        public StackFrameList<object> ObjectStack;
    }

    internal unsafe ref struct StackFrame
    {
        //In order for the linked list to work. This also stores index of StackSegment.
        public nint Head;
        public Span<nint> Last; //Last StackFrame's Head field.
        public Span<double> NumberFrame;
        public Span<object> ObjectFrame;

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
        private int _currentHead = 0;

        public StackManager()
        {
            _segments.Add(new()
            {
                NumberStack = new(Options.StackSegmentSize),
                ObjectStack = new(Options.StackSegmentSize),
            });
        }

        public StackFrame Allocate(int numSize, int objSize)
        {
            Debug.Assert(numSize < Options.MaxSingleFunctionStackSize);
            Debug.Assert(objSize < Options.MaxSingleFunctionStackSize);
            Debug.Assert(_currentHead == 0);
            var s = _segments[0];
            return new StackFrame
            {
                Head = 0,
                Last = default,
                NumberFrame = s.NumberStack.Push(numSize),
                ObjectFrame = s.ObjectStack.Push(numSize),
            };
        }

        public StackFrame Allocate(ref StackFrame lastFrame, int numSize, int objSize, bool onSameSeg)
        {
            Debug.Assert(numSize < Options.MaxSingleFunctionStackSize);
            Debug.Assert(objSize < Options.MaxSingleFunctionStackSize);
            Debug.Assert(_currentHead == (int)lastFrame.Head);

            var s = _segments[(int)lastFrame.Head];

            //TODO should check total length instead of numSize and objSize
            var argNSize = lastFrame.MetaData.SigTotalVLength;
            var argOSize = lastFrame.MetaData.SigTotalOLength;
            //Include one more element at the beginning allows TryExtend to calculate starting point using span[0].
            var argN = lastFrame.NumberFrame.Slice(lastFrame.MetaData.SigNumberOffset - 1, argNSize + 1);
            var argO = lastFrame.ObjectFrame.Slice(lastFrame.MetaData.SigObjectOffset - 1, argOSize + 1);

            if (!s.NumberStack.CheckSpace(numSize) || !s.ObjectStack.CheckSpace(objSize))
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
                    int totalNewNumSize = numSize + lastFrame.MetaData.SigNumberOffset;
                    int totalNewObjSize = objSize + lastFrame.MetaData.SigObjectOffset;
                    Span<nint> iter = lastFrame.Last;
                    int numOffsetOnOldSeg = s.NumberStack.GetIndex(ref lastFrame.NumberFrame[0]);
                    int objOffsetOnOldSeg = s.ObjectStack.GetIndex(ref lastFrame.ObjectFrame[0]);
                    if (lastFrame.OnSameSegment)
                    {
                        while (true)
                        {
                            unsafe
                            {
                                ref var f = ref StackFrame.GetLast(ref iter[0]);
                                Debug.Assert((int)f.Head == _currentHead);
                                totalNewNumSize += f.MetaData.SigNumberOffset;
                                totalNewObjSize += f.MetaData.SigObjectOffset;
                                iter = f.Last;
                                if (!f.OnSameSegment || iter.Length == 0)
                                {
                                    numOffsetOnOldSeg = s.NumberStack.GetIndex(ref f.NumberFrame[0]);
                                    objOffsetOnOldSeg = s.ObjectStack.GetIndex(ref f.ObjectFrame[0]);
                                    break;
                                }
                            }
                        }
                    }
                    totalNewNumSize = Math.Max(totalNewNumSize + Options.MaxSingleFunctionStackSize, Options.StackSegmentSize);
                    totalNewObjSize = Math.Max(totalNewNumSize + Options.MaxSingleFunctionStackSize, Options.StackSegmentSize);

                    //Allocate new segment.
                    var newSegment = new StackSegment
                    {
                        NumberStack = new(totalNewNumSize),
                        ObjectStack = new(totalNewObjSize),
                    };
                    s.NumberStack.CopyTo(newSegment.NumberStack);
                    s.ObjectStack.CopyTo(newSegment.ObjectStack);

                    //Iterate and fix all frames.
                    iter = lastFrame.Last;
                    if (lastFrame.OnSameSegment)
                    {
                        while (iter.Length > 0)
                        {
                            unsafe
                            {
                                ref var f = ref StackFrame.GetLast(ref iter[0]);
                                var newObjIndex = s.ObjectStack.GetIndex(ref f.ObjectFrame[0]) - objOffsetOnOldSeg;
                                f.ObjectFrame = newSegment.ObjectStack.FromIndex(newObjIndex, f.ObjectFrame.Length);
                                var newNumIndex = s.NumberStack.GetIndex(ref f.NumberFrame[0]) - numOffsetOnOldSeg;
                                f.NumberFrame = newSegment.NumberStack.FromIndex(newNumIndex, f.NumberFrame.Length);
                                if (!f.OnSameSegment) break;
                                iter = f.Last;
                            }
                        }
                    }
                    newSegment.NumberStack.Reset(lastFrame.NumberFrame);
                    newSegment.ObjectStack.Reset(lastFrame.ObjectFrame);

                    s = newSegment;
                    argN = lastFrame.NumberFrame.Slice(lastFrame.MetaData.SigNumberOffset - 1, argNSize + 1);
                    argO = lastFrame.ObjectFrame.Slice(lastFrame.MetaData.SigObjectOffset - 1, argOSize + 1);
                    //Fall through and allocate the new frame.
                }
                else
                {
                    //Allocate new frame on a new segment.
                    if (++_currentHead == _segments.Count)
                    {
                        _segments.Add(new()
                        {
                            NumberStack = new(Options.StackSegmentSize),
                            ObjectStack = new(Options.StackSegmentSize),
                        });
                    }
                    s = _segments[_currentHead];
                    var nframe = s.NumberStack.Push(numSize + argNSize);
                    var oframe = s.ObjectStack.Push(objSize + argOSize);

                    //Copy args from old frame.
                    argN[1..].CopyTo(nframe);
                    argO[1..].CopyTo(oframe);
                    return new StackFrame
                    {
                        Head = _currentHead,
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
            s.NumberStack.TryExtend(ref argN, argNSize + numSize + 1);
            s.ObjectStack.TryExtend(ref argO, argOSize + objSize + 1);
            return new StackFrame
            {
                Head = _currentHead,
                Last = MemoryMarshal.CreateSpan(ref lastFrame.Head, 1),
                NumberFrame = argN[1..],
                ObjectFrame = argO[1..],
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

        public void Deallocate(ref StackFrame frame)
        {
            var s = _segments[(int)frame.Head];
            if (frame.Last.Length == 0)
            {
                s.NumberStack.Clear();
                s.ObjectStack.Clear();
            }
            else
            {
                unsafe
                {
                    ref StackFrame lastFrame = ref StackFrame.GetLast(ref frame.Last[0]);
                    s.NumberStack.Reset(lastFrame.NumberFrame);
                    s.ObjectStack.Reset(lastFrame.ObjectFrame);
                }
            }
        }

        private SerializedStackFrame SerializeFrameInternal(ref StackFrame frame)
        {
            var seg = _segments[(int)frame.Head];
            return new SerializedStackFrame
            {
                Head = (int)frame.Head,
                NumberFrame = seg.NumberStack.ToArraySegment(frame.NumberFrame),
                ObjectFrame = seg.ObjectStack.ToArraySegment(frame.ObjectFrame),
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
