using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    //TODO rename this source file

    internal struct InterFrameSignatureState
    {
        public StackSignature Type;
        public int Offset;
        public int VLength;

        public int TotalLength => Type.FLength + VLength;
    }

    internal struct StackSignatureState
    {
        public int TypeId;
        public int Offset;
        public int FLength;
        public int VLength;

        //Unspecialized state. Used by C#-Lua calls.
        public StackSignatureState(int offset, int length)
        {
            TypeId = (int)WellKnownStackSignature.EmptyV;
            Offset = offset;
            FLength = 0;
            VLength = length;
        }

        public StackSignatureState(in StackFrameValues values, StackSignature inputSignature, int vlength,
            StackSignature[] newSigTypes, int newSigTypeId)
        {
            if (inputSignature.GlobalId == newSigTypes[newSigTypeId].GlobalId)
            {
                TypeId = newSigTypeId;
                Offset = 0;
                FLength = inputSignature.FLength;
                VLength = vlength;
                return;
            }
            TypeId = -1;
            Offset = 0;
            FLength = 0;
            VLength = vlength;
            AdjustRight(in values, -1, inputSignature, newSigTypes, newSigTypeId);
        }

        public int TotalLength
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => FLength + VLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            this = default;
        }

        public void AdjustLeft(in StackFrameValues values, StackSignature[] sigTypes, int newSigType)
        {
            Debug.Assert(sigTypes[newSigType].FLength >= sigTypes[TypeId].FLength);

            //TODO in some cases we need to update values
            //Type = newSigType;
            //return Offset -= newSigType.FLength - Type.FLength;
            throw new NotImplementedException();
            //Don't clear v length.
        }

        //Vararg part is kept if the new type contains vararg, and discarded otherwise.
        public bool AdjustRight(in StackFrameValues values, int oldSigIndex,
            StackSignature oldSigType, StackSignature[] newSigTypes, int newSigType)
        {
            if (newSigType == oldSigIndex)
            {
                //Same type.
                return true;
            }
            if (!oldSigType.IsCompatibleWith(newSigTypes[newSigType]))
            {
                //Not compatible.
                if (!newSigTypes[newSigType].IsUnspecialized)
                {
                    //Target is not unspecialized. We can do nothing here.
                    return false;
                }

                //Target is unspecialized. We can make them compatible.
                Debug.Assert(!oldSigType.IsUnspecialized);
                oldSigType.AdjustStackToUnspecialized(in values);
            }

            var diff = newSigTypes[newSigType].FLength - oldSigType.FLength;

            //Fill nils if necessary.
            if (diff > VLength)
            {
                values.Span.Slice(Offset + TotalLength, diff).Fill(TypedValue.Nil);
                VLength = 0;
            }
            else
            {
                //Update variant part length for WriteVararg to work properly.
                VLength -= diff;
            }

            TypeId = newSigType;

            if (!newSigTypes[TypeId].Vararg.HasValue)
            {
                DiscardVararg(in values);
            }
            return true;
        }

        public void MoveVararg(in StackFrameValues values, List<TypedValue> storage, ref StackFrameVarargInfo varargInfo)
        {
            varargInfo.VarargStart = storage.Count;
            varargInfo.VarargLength = VLength;
            VLength = 0;

            var start = Offset + FLength;
            for (int i = 0; i < varargInfo.VarargLength; ++i)
            {
                storage.Add(values[start + i]);
                values[start + i].Object = null;
            }
        }

        private void DiscardVararg(in StackFrameValues values)
        {
            if (VLength > 0)
            {
                var start = Offset + FLength;
                for (int i = 0; i < VLength; ++i)
                {
                    values[start + i].Object = null;
                }
            }
        }
    }

    internal struct StackSignatureAdjustment
    {
        public readonly void Adjust(ref StackSignatureState state)
        {
            //1. adjust right, fast path (unspecialized -> unspecialized only)
            //   change offset, change vlength, set type
            //2. adjust right, slow path
            //   may involve value conversion/move
            //   change offset, change vlength, set type

            //3. adjust left, fast path (no move)
            //   change offset and type
            //4. adjust left, slow path
            //   may involve value move (no conversion)
            //     the move can be complicated, as we are using the layout defined by StackSigBuilder
            //     better specify a list of moved items and their type
            //     in most cases, this should only move a single value, so not too slow
            //   change offset and type


            //as an optimization, at least part of this struct should be stored in instruction stream
        }
    }
}
