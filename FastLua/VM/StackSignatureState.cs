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

    internal struct StackSignatureState
    {
        public StackSignature Type;
        public int Offset;
        public int VLength;

        //New sig containing arguments, at the beginning of the frame (Offset = 0).
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StackSignatureState(StackSignature inputSigType, int inputVarg)
        {
            Type = inputSigType;
            Offset = 0;
            VLength = inputVarg;
        }

        //Unspecialized state. Used by C#-Lua calls.
        public StackSignatureState(int offset, int length)
        {
            Type = StackSignature.EmptyV;
            Offset = offset;
            VLength = length;
        }

        public int TotalSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => VLength + Type.FixedSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            this = default;
        }

        public void AdjustLeft(in StackFrameValues values, StackSignature newSigType)
        {
            //Debug.Assert(newSigType.FixedSize >= Type.FixedSize);

            //TODO in some cases we need to update values
            //Type = newSigType;
            //return Offset -= newSigType.FLength - Type.FLength;
            throw new NotImplementedException();
            //Don't clear v length.
        }

        //Vararg part is always kept. Caller must use DiscardVararg/MoveVararg to clear them
        //if the new type contains no vararg.
        public bool AdjustRight(in StackFrameValues values, StackSignature newSigType)
        {
            if (newSigType.GlobalId == Type.GlobalId)
            {
                //Same type.
                return true;
            }
            if (!Type.IsCompatibleWith(newSigType))
            {
                //Not compatible.

                if (!newSigType.IsUnspecialized)
                {
                    //Target is not unspecialized. Adjust in slow path.
                    return Type.CheckAndAdjustStackToType(in values, newSigType, ref VLength);
                }

                //Two unspecialized types are always compatible.
                Debug.Assert(!Type.IsUnspecialized);

                //Target is unspecialized. We can make them compatible by AdjustStackToUnspecialized.
                //This is hopefully faster than CheckAndAdjustStackToType and is ensured to succeed.
                Type.AdjustStackToUnspecialized(in values, ref VLength);
                Type = newSigType;
                VLength -= newSigType.ElementCount;
                return true;
            }

            //Two types are compatible. We simply need to update VLength (or fill with nils).

            //It shouldn't matter whether we use element count or fixed size. They are the same.
            var diff = newSigType.ElementCount - Type.ElementCount;
            Debug.Assert(diff == newSigType.FixedSize - Type.FixedSize);

            //Fill nils if necessary.
            if (diff > VLength)
            {
                values.Span.Slice(Offset + TotalSize, diff - VLength).Fill(TypedValue.Nil);
                VLength = 0;
            }
            else
            {
                //Update variant part length for WriteVararg to work properly.
                VLength -= diff;
            }

            Type = newSigType;
            return true;
        }

        public void MoveVararg(in StackFrameValues values, List<TypedValue> storage, ref StackFrameVarargInfo varargInfo)
        {
            if (!Type.Vararg.HasValue)
            {
                DiscardVararg(in values);
                return;
            }
            Debug.Assert(Type.Vararg.HasValue);

            varargInfo.VarargStart = storage.Count;
            varargInfo.VarargLength = VLength;

            var start = Offset + Type.FixedSize;
            for (int i = 0; i < varargInfo.VarargLength; ++i)
            {
                storage.Add(values[start + i]);
                values[start + i].Object = null;
            }
        }

        public void DiscardVararg(in StackFrameValues values)
        {
            if (VLength > 0)
            {
                var start = Offset + Type.FixedSize;
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
