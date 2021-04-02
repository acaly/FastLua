using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    internal class Thread
    {
        public StackManager Stack = new();
        public StringBuilder StringBuilder = new();
        public List<TypedValue> VarargStack = new();
        public int VarargTotalLength = 0;
        
        //Signature region.

        public SignatureDesc SigDesc;

        public int SigNumberOffset; //Start of sig block.
        public int SigObjectOffset;
        public int SigVOLength; //Length of vararg part.
        public int SigVVLength;

        public int SigTotalOLength => SigDesc.SigFOLength + SigVOLength;
        public int SigTotalVLength => SigDesc.SigFVLength + SigVVLength;

        public void ClearSigBlock()
        {
            SigVOLength = SigVVLength = 0;
            SigDesc = SignatureDesc.Null;
        }

        public void SetSigBlock(ref SignatureDesc desc, int o, int v)
        {
            SigObjectOffset = o;
            SigNumberOffset = v;
            SigVOLength = 0;
            SigVVLength = 0;
            SigDesc = desc;
        }

        //Adjust sig block. This operation handles sig block generated inside the same function
        //so it should never fail (or it's a program error), and we don't really need to check.
        public void ResizeSigBlockLeft(ref SignatureDesc desc, int o, int v)
        {
            //0 is the Null signature, which is different from Empty. Null can only be created
            //from ClearSigBlock (when starting a new function or using CALLC instruction).
            if (SigDesc.SigTypeId == 0)
            {
                //New sig block at given place.
                //Note that the (o,v) is the last item (inclusive) in the block.
                SigObjectOffset = o - desc.SigFOLength;
                SigNumberOffset = v - desc.SigFVLength;
                SigDesc = desc;
            }
            else
            {
                Debug.Assert(desc.SigFVLength >= SigDesc.SigFVLength);
                Debug.Assert(desc.SigFOLength >= SigDesc.SigFOLength);

                //Extend to left.
                SigNumberOffset -= desc.SigFVLength - SigDesc.SigFVLength;
                SigObjectOffset -= desc.SigFOLength - SigDesc.SigFOLength;
                SigDesc = desc;
            }
            //Don't clear vv and vo length.
        }

        //Adjust the sig block with given type without changing its starting position.
        //If varargStorage is not null, copy the vararg part to the separate list.
        public bool TryAdjustSigBlockRight(ref StackFrame stack, ref SignatureDesc desc,
            List<TypedValue> varargStorage, out int varargCount)
        {
            if (desc.SigTypeId == SigDesc.SigTypeId)
            {
                //Same type.
                WriteVararg(ref stack, varargStorage, out varargCount);
                return true;
            }
            if (!SigDesc.SigType.IsCompatibleWith(desc.SigTypeId))
            {
                //Not compatible.
                if (!desc.SigType.IsUnspecialized)
                {
                    //Target is not unspecialized. We can do nothing here.
                    varargCount = 0;
                    return false;
                }

                //Target is unspecialized. We can make them compatible.
                Debug.Assert(!SigDesc.SigType.IsUnspecialized);
                SigDesc.SigType.AdjustStackToUnspecialized();
            }

            var diffO = desc.SigFOLength - SigDesc.SigFOLength;
            var diffV = desc.SigFVLength - SigDesc.SigFVLength;

            //Fill nils if necessary.
            if (diffO > 0)
            {
                stack.ObjectFrame.Slice(SigObjectOffset + SigTotalOLength, diffO).Clear();
            }
            if (diffV > 0)
            {
                stack.NumberFrame.Slice(SigNumberOffset + SigTotalVLength, diffV).Fill(TypedValue.Nil.Number);
            }
            SigDesc = desc;
            SigVOLength -= diffO; //Update variant part length for WriteVararg to work properly.
            SigVVLength -= diffV;
            WriteVararg(ref stack, varargStorage, out varargCount);
            return true;
        }

        private void WriteVararg(ref StackFrame stack, List<TypedValue> storage, out int count)
        {
            if (storage is null || !SigDesc.SigType.Vararg.HasValue)
            {
                count = 0;
                return;
            }
            var vo = SigDesc.HasVO;
            var vv = SigDesc.HasVV;
            int start;
            if (vv && vo)
            {
                //Unspecialized vararg.
                Debug.Assert(SigNumberOffset == SigObjectOffset);
                Debug.Assert(SigDesc.SigFVLength == SigDesc.SigFOLength);
                Debug.Assert(SigVOLength == SigVVLength);
                start = SigNumberOffset + SigDesc.SigFVLength;
                count = SigVVLength;
            }
            else if (vo)
            {
                start = SigObjectOffset + SigDesc.SigFOLength;
                count = SigVOLength;
            }
            else
            {
                Debug.Assert(vv);
                start = SigNumberOffset + SigDesc.SigFVLength;
                count = SigVVLength;
            }
            for (int i = 0; i < count; ++i)
            {
                storage.Add(new TypedValue
                {
                    Number = stack.NumberFrame[start + i],
                    Object = stack.ObjectFrame[start + i],
                });
            }
        }

        public void AppendVarargSig(VMSpecializationType type, int count)
        {
            //There can only be one vararg block.
            Debug.Assert(SigVOLength == 0 && SigVVLength == 0);
            SigDesc = SigDesc.WithVararg(type);
            var (v, o) = type.GetStorageType();
            if (v) SigVVLength = count;
            if (o) SigVOLength = count;
        }
    }
}
