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

        public void AdjustSigBlockLeft(ref SignatureDesc desc, (int o, int v) emptySigPosition)
        {
            //Currently only support sig id 0 (no vararg) and sig id 1(vararg)
            throw new NotImplementedException();
        }

        public void AdjustSigBlockRightEmpty()
        {
            //Similar to TryAdjustSigBlockRight but adjust to empty sig (clear sig info)
            throw new NotImplementedException();
        }

        public bool TryAdjustSigBlockRight(ref SignatureDesc desc)
        {
            //Currently only support sig id 0 (no vararg) and sig id 1(vararg)
            throw new NotImplementedException();
        }

        public bool TryAdjustSigBlockRight(ref SignatureDesc desc, List<TypedValue> varargStorage, out int varargCount)
        {
            //similar to TryAdjustSigBlockRight, but instead of removing more values from right,
            //it moves more values into a vararg storage.
            throw new NotImplementedException();
        }

        public (int o, int v) AppendSig(ref StackMetaData newSigData)
        {
            //Append slots after current sig based on another frame's sig data.
            //Used by VM's RETN to pass return values back to caller.
            //Return index of the new region for copying.
            throw new NotImplementedException();
        }

        public void AppendVarargSig(VMSpecializationType type, int count)
        {
            //Unspecialized VARG must be used on unspecialized sig block.
            Debug.Assert(MetaData.SigNumberOffset == MetaData.SigObjectOffset);

            throw new NotImplementedException();
        }
    }

    internal struct SignatureDesc
    {
        public ulong SigTypeId;
        public StackSignature SigType;
        public int SigFOLength, SigFVLength; //Length of fixed part.
        public bool HasVO, HasVV;

        public static readonly SignatureDesc Empty = new SignatureDesc
        {
            SigTypeId = 0,
            SigType = null,
            SigFOLength = 0,
            SigFVLength = 0,
            HasVO = false,
            HasVV = false,
        };
    }

    //This struct stores other per-frame data (in addition to stack).
    internal struct StackMetaData
    {
        public Proto Func;

        //Signature region.

        //TODO offset should also
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

        public StackFrame Allocate(ref StackFrame lastFrame, int numSize, int objSize)
        {
            Debug.Assert(numSize < Options.MaxSingleFunctionStackSize);
            Debug.Assert(objSize < Options.MaxSingleFunctionStackSize);

            var s = _segments[_currentHead];

            //TODO should check total length instead of numSize and objSize
            var argNSize = lastFrame.MetaData.SigTotalVLength;
            var argOSize = lastFrame.MetaData.SigTotalOLength;
            var argN = lastFrame.NumberFrame.Slice(lastFrame.MetaData.SigNumberOffset, argNSize);
            var argO = lastFrame.ObjectFrame.Slice(lastFrame.MetaData.SigObjectOffset, argOSize);

            if (!s.NumberStack.CheckSpace(numSize) || !s.ObjectStack.CheckSpace(objSize))
            {
                if (++_currentHead == _segments.Count)
                {
                    _segments.Add(new()
                    {
                        NumberStack = new(Options.StackSegmentSize),
                        ObjectStack = new(Options.StackSegmentSize),
                    });
                }
                s = _segments[_currentHead];
                var nframe = s.NumberStack.Push(numSize + argNSize); //TODO this may overflow
                var oframe = s.ObjectStack.Push(objSize + argOSize);

                //Copy args from old frame.
                argN.CopyTo(nframe);
                argO.CopyTo(oframe);

                return new StackFrame
                {
                    Head = _currentHead,
                    Last = MemoryMarshal.CreateSpan(ref lastFrame.Head, 1),
                    NumberFrame = nframe,
                    ObjectFrame = oframe,
                };
            }
            else
            {
                //Don't need to copy args. Use extend to allocate frame after the current one.
                s.NumberStack.TryExtend(ref argN, argNSize + numSize);
                s.ObjectStack.TryExtend(ref argO, argOSize + objSize);
                return new StackFrame
                {
                    Head = _currentHead,
                    Last = MemoryMarshal.CreateSpan(ref lastFrame.Head, 1),
                    NumberFrame = argN,
                    ObjectFrame = argO,
                };
            }
        }

        public void Deallocate(ref StackFrame lastFrame)
        {
            throw new NotImplementedException();
            //Pop from last segment.
            //Remove last segment (move to cache) if it's empty
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
