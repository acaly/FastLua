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

        public int SigOffset; //Start of sig block.
        public int SigVLength; //Length of vararg part.

        public int SigTotalLength => SigDesc.SigFLength + SigVLength;

        public void ClearSigBlock()
        {
            SigVLength = 0;
            SigDesc = SignatureDesc.Null;
        }

        public void SetSigBlock(ref SignatureDesc desc, int pos)
        {
            //Keep VLength.
            SigOffset = pos;
            SigDesc = desc;
        }

        //Set the sig to contain only a vararg part.
        //This is always followed by an adjustment.
        public void SetSigBlockVararg(ref SignatureDesc desc, int pos, int length)
        {
            Debug.Assert(desc.SigFLength == 0);
            SigOffset = pos;
            SigDesc = desc;
            SigVLength = length;
        }

        //Adjust sig block. This operation handles sig block generated inside the same function
        //so it should never fail (or it's a program error), and we don't really need to check.
        public void ResizeSigBlockLeft(ref SignatureDesc desc, int pos)
        {
            //0 is the Null signature, which is different from Empty. Null can only be created
            //from ClearSigBlock (when starting a new function or using CALLC instruction).
            if (SigDesc.SigTypeId == 0)
            {
                //New sig block at given place.
                SigOffset = pos;
                SigDesc = desc;
            }
            else
            {
                Debug.Assert(desc.SigFLength >= SigDesc.SigFLength);

                //Extend to left.
                SigOffset -= desc.SigFLength - SigDesc.SigFLength;
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
            if (!SigDesc.SigType.IsCompatibleWith(desc.SigType))
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

            var diff = desc.SigFLength - SigDesc.SigFLength;

            //Fill nils if necessary.
            if (desc.SigFLength > SigDesc.SigFLength + SigVLength)
            {
                stack.ValueFrame.Slice(SigOffset + SigTotalLength, diff).Fill(TypedValue.Nil);
            }

            //Update variant part length for WriteVararg to work properly.
            SigVLength -= desc.SigFLength - SigDesc.SigFLength;

            SigDesc = desc;

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
            Debug.Assert(SigDesc.HasV);
            int start;
            start = SigOffset + SigDesc.SigFLength;
            count = SigVLength;
            for (int i = 0; i < count; ++i)
            {
                storage.Add(stack.ValueFrame[start + i]);
            }
        }
    }
}
