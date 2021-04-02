using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static FastLua.VM.VMHelper;

namespace FastLua.VM
{
    internal partial class LuaInterpreter
    {
        //Static info for interpreter that does not need any update when entering/exiting frames.

        public static void Execute(Thread thread, LClosure closure)
        {
            //Currently cannot resume a yielded thread.
            var stack = thread.Stack.Allocate(closure.Proto.NumStackSize, closure.Proto.ObjStackSize);
            stack.MetaData = new StackMetaData
            {
                Func = closure.Proto,
                SigDesc = default,
                SigNumberOffset = closure.Proto.SigRegionOffsetV,
                SigObjectOffset = closure.Proto.SigRegionOffsetO,
                SigVOLength = 0,
                SigVVLength = 0,
                VarargStart = 0,
                VarargLength = 0,
                VarargType = default,
            };
            InterpreterLoop_Template(0, thread, ref stack);
            thread.Stack.Deallocate(ref stack);
        }

        private static partial void InterpreterLoop(int startPc, Thread thread, ref StackFrame stack);

        [InlineSwitch(typeof(LuaInterpreter))]
        private static void InterpreterLoop_Template(int startPc, Thread thread, ref StackFrame stack)
        {
            var inst = stack.MetaData.Func.Instructions;
            var pc = startPc;
            int lastWriteO = 0, lastWriteV = 0;
            while (true)
            {
                var ii = inst[pc++];
                switch ((Opcodes)(ii >> 24))
                {
                default:
                    //This is only a placeholder and for debugging purpose.
                    Nop(thread, ref stack, ref pc, ii, ref lastWriteO, ref lastWriteV);
                    Comparison(thread, ref stack, ref pc, ii, ref lastWriteO, ref lastWriteV);
                    Test(thread, ref stack, ref pc, ii, ref lastWriteO, ref lastWriteV);
                    Unary(thread, ref stack, ref pc, ii, ref lastWriteO, ref lastWriteV);
                    Binary(thread, ref stack, ref pc, ii, ref lastWriteO, ref lastWriteV);
                    Constant(thread, ref stack, ref pc, ii, ref lastWriteO, ref lastWriteV);
                    Upval(thread, ref stack, ref pc, ii, ref lastWriteO, ref lastWriteV);
                    Table(thread, ref stack, ref pc, ii, ref lastWriteO, ref lastWriteV);
                    Call(thread, ref stack, ref pc, ii, ref lastWriteO, ref lastWriteV);
                    Ret(thread, ref stack, ref pc, ii, ref lastWriteO, ref lastWriteV, out var ret);
                    if (ret) return;
                    break;
                }
            }
        }

        private static void JumpToFallback(Thread thread, ref StackFrame stack)
        {
            throw new NotImplementedException();
        }

        [InlineSwitchCase]
        private static void Nop(Thread thread, ref StackFrame stack,
            ref int pc, uint ii, ref int lastWriteO, ref int lastWriteV)
        {
            switch ((Opcodes)(ii >> 24))
            {
            case Opcodes.NOP:
                break;
            }
        }

        [InlineSwitchCase]
        private static void Comparison(Thread thread, ref StackFrame stack,
            ref int pc, uint ii, ref int lastWriteO, ref int lastWriteV)
        {
            switch ((Opcodes)(ii >> 24))
            {
            case Opcodes.ISLT:
            {
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                if (CompareValue(stack.GetU(a), stack.GetU(b)) < 0)
                {
                    pc += (sbyte)(byte)(ii & 0xFF);
                }
                break;
            }
            case Opcodes.ISLE:
            {
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                if (CompareValue(stack.GetU(a), stack.GetU(b)) <= 0)
                {
                    pc += (sbyte)(byte)(ii & 0xFF);
                }
                break;
            }
            case Opcodes.ISEQ:
            {
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                if (CompareValue(stack.GetU(a), stack.GetU(b)) == 0)
                {
                    pc += (sbyte)(byte)(ii & 0xFF);
                }
                break;
            }
            case Opcodes.ISNE:
            {
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                if (CompareValueNE(stack.GetU(a), stack.GetU(b)))
                {
                    pc += (sbyte)(byte)(ii & 0xFF);
                }
                break;
            }
            }
        }

        [InlineSwitchCase]
        private static void Test(Thread thread, ref StackFrame stack,
            ref int pc, uint ii, ref int lastWriteO, ref int lastWriteV)
        {
            switch ((Opcodes)(ii >> 24))
            {
            case Opcodes.ISTC:
            {
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                if (stack.ToBoolU(b))
                {
                    stack.CopyUU(b, a);
                    pc += (sbyte)(byte)(ii & 0xFF);
                }
                lastWriteO = lastWriteV = a;
                break;
            }
            case Opcodes.ISFC:
            {
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                if (!stack.ToBoolU(b))
                {
                    stack.CopyUU(b, a);
                    pc += (sbyte)(byte)(ii & 0xFF);
                }
                lastWriteO = lastWriteV = a;
                break;
            }
            }
        }

        [InlineSwitchCase]
        private static void Unary(Thread thread, ref StackFrame stack,
            ref int pc, uint ii, ref int lastWriteO, ref int lastWriteV)
        {
            switch ((Opcodes)(ii >> 24))
            {
            case Opcodes.MOV:
            {
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                stack.CopyUU(b, a);
                lastWriteO = lastWriteV = a;
                break;
            }
            case Opcodes.NOT:
            {
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                stack.SetU(a, stack.ToBoolU(b) ? TypedValue.False : TypedValue.True);
                lastWriteO = lastWriteV = a;
                break;
            }
            case Opcodes.NEG:
            {
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                switch (stack.GetTypeU(b))
                {
                case VMSpecializationType.Int:
                    stack.SetU(a, TypedValue.MakeInt(-stack.GetIntU(b)));
                    break;
                case VMSpecializationType.Double:
                    stack.SetU(a, TypedValue.MakeDouble(-stack.GetDoubleU(b)));
                    break;
                default:
                    stack.SetU(a, UnaryNeg(stack.GetU(b)));
                    break;
                }
                lastWriteO = lastWriteV = a;
                break;
            }
            case Opcodes.LEN:
            {
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                stack.SetU(a, UnaryLen(stack.GetU(b)));
                lastWriteO = lastWriteV = a;
                break;
            }
            }
        }

        [InlineSwitchCase]
        private static void Binary(Thread thread, ref StackFrame stack,
            ref int pc, uint ii, ref int lastWriteO, ref int lastWriteV)
        {
            switch ((Opcodes)(ii >> 24))
            {
            case Opcodes.ADD:
            {
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                int c = (int)(ii & 0xFF);
                stack.SetU(a, Add(stack.GetU(b), stack.GetU(c)));
                lastWriteO = lastWriteV = a;
                break;
            }
            case Opcodes.SUB:
            {
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                int c = (int)(ii & 0xFF);
                stack.SetU(a, Sub(stack.GetU(b), stack.GetU(c)));
                lastWriteO = lastWriteV = a;
                break;
            }
            case Opcodes.MUL:
            {
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                int c = (int)(ii & 0xFF);
                stack.SetU(a, Mul(stack.GetU(b), stack.GetU(c)));
                lastWriteO = lastWriteV = a;
                break;
            }
            case Opcodes.DIV:
            {
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                int c = (int)(ii & 0xFF);
                stack.SetU(a, Div(stack.GetU(b), stack.GetU(c)));
                lastWriteO = lastWriteV = a;
                break;
            }
            case Opcodes.MOD:
            {
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                int c = (int)(ii & 0xFF);
                stack.SetU(a, Mod(stack.GetU(b), stack.GetU(c)));
                lastWriteO = lastWriteV = a;
                break;
            }
            case Opcodes.POW:
            {
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                int c = (int)(ii & 0xFF);
                stack.SetU(a, Pow(stack.GetU(b), stack.GetU(c)));
                lastWriteO = lastWriteV = a;
                break;
            }
            case Opcodes.CAT:
            {
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                int c = (int)(ii & 0xFF);

                var sb = thread.StringBuilder;
                sb.Clear();
                for (int i = b; i < c; ++i)
                {
                    WriteString(sb, stack.GetU(i));
                }
                stack.SetU(a, TypedValue.MakeString(sb.ToString()));
                lastWriteO = lastWriteV = a;
                break;
            }
            }
        }

        [InlineSwitchCase]
        private static void Constant(Thread thread, ref StackFrame stack,
            ref int pc, uint ii, ref int lastWriteO, ref int lastWriteV)
        {
            switch ((Opcodes)(ii >> 24))
            {
            case Opcodes.K:
            {
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                stack.SetU(a, stack.MetaData.Func.ConstantsU[b]);
                lastWriteO = lastWriteV = a;
                break;
            }
            }
        }

        [InlineSwitchCase]
        private static void Upval(Thread thread, ref StackFrame stack,
            ref int pc, uint ii, ref int lastWriteO, ref int lastWriteV)
        {
            switch ((Opcodes)(ii >> 24))
            {
            case Opcodes.UGET:
            {
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                int c = (int)(ii & 0xFF);
                stack.SetU(a, stack.GetUpvalOU(b, c));
                lastWriteO = lastWriteV = a;
                break;
            }
            case Opcodes.USET:
            {
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                int c = (int)(ii & 0xFF);
                stack.SetUpvalOU(b, c, stack.GetU(a));
                break;
            }
            case Opcodes.UNEW:
            {
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                stack.ObjectFrame[a] = new TypedValue[b];
                break;
            }
            case Opcodes.FNEW:
            {
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                int u = stack.MetaData.SigObjectOffset;
                int n = stack.MetaData.SigVOLength;
                var closure = new LClosure
                {
                    Proto = stack.MetaData.Func.ChildFunctions[b],
                    UpvalLists = new TypedValue[n][],
                };
                var src = MemoryMarshal.CreateSpan(ref Unsafe.As<object, TypedValue[]>(ref stack.ObjectFrame[u]), n);
                src.CopyTo(closure.UpvalLists.AsSpan());
                stack.SetU(a, TypedValue.MakeLClosure(closure));
                lastWriteO = lastWriteV = a;
                break;
            }
            }
        }

        [InlineSwitchCase]
        private static void Table(Thread thread, ref StackFrame stack,
            ref int pc, uint ii, ref int lastWriteO, ref int lastWriteV)
        {
            switch ((Opcodes)(ii >> 24))
            {
            case Opcodes.TNEW:
            {
                int a = (int)((ii >> 16) & 0xFF);
                stack.SetU(a, TypedValue.MakeTable(new Table()));
                lastWriteO = lastWriteV = a;
                break;
            }
            case Opcodes.TGET:
            {
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                int c = (int)(ii & 0xFF);
                var t = (Table)stack.GetU(b).Object;
                stack.SetU(a, t.Get(stack.GetU(c)));
                lastWriteO = lastWriteV = a;
                break;
            }
            case Opcodes.TSET:
            {
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                int c = (int)(ii & 0xFF);
                var t = (Table)stack.GetU(b).Object;
                t.Set(stack.GetU(c), stack.GetU(a));
                break;
            }
            }
        }

        [InlineSwitchCase]
        private static void Call(Thread thread, ref StackFrame stack,
            ref int pc, uint ii, ref int lastWriteO, ref int lastWriteV)
        {
            switch ((Opcodes)(ii >> 24))
            {
            case Opcodes.JMP:
            {
                pc += (int)(short)(ushort)(ii & 0xFFFF);
                break;
            }
            case Opcodes.CALL:
            case Opcodes.CALLC:
            {
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                int c = (int)(ii & 0xFF);

                var thisFunc = stack.MetaData.Func;

                //Adjust current sig block at the left side.
                //This allows to merge other arguments that have already been pushed before.
                //If current sig is empty, set the starting point based on lastWrite.
                ref var argSig = ref thisFunc.SigDesc[b];
                stack.ResizeSigBlockLeft(ref argSig, lastWriteO + 1 - argSig.SigFOLength, lastWriteV + 1 - argSig.SigFVLength);

                var newFuncP = stack.ObjectFrame[a];
                if (newFuncP is LClosure lc)
                {
                    //Setup new stack frame and call.
                    var proto = lc.Proto;

                    var onSameSegment = stack.MetaData.Func.SigDesc[c].SigType.Vararg.HasValue;

                    //This allocates the new frame's stack, which may overlap with the current (to pass args)
                    //TODO seems a lot of work here in Allocate. Should try to simplify.
                    //Especially the sig check.
                    var newStack = thread.Stack.Allocate(ref stack, proto.NumStackSize, proto.ObjStackSize, onSameSegment);

                    //Adjust argument list according to the requirement of the callee.
                    //Also remove vararg into separate stack.
                    int varargStart = thread.VarargTotalLength;
                    if (!newStack.TryAdjustSigBlockRight(ref proto.ParameterSig, thread.VarargStack, out var varargLength))
                    {
                        //Cannot adjust argument list. Need to call a deoptimized version of the target function.
                        //This can be done by re-executing the CALL instruction from the deoptimized version.
                        pc -= 1;
                        JumpToFallback(thread, ref stack);
                        break;
                    }

                    //Push closure's upvals.
                    Debug.Assert(lc.UpvalLists.Length <= proto.LocalRegionOffsetO - proto.UpvalRegionOffset);
                    for (int i = 0; i < lc.UpvalLists.Length; ++i)
                    {
                        newStack.ObjectFrame[proto.UpvalRegionOffset + i] = lc.UpvalLists[i];
                        //We could also set types in value frame, but this region should never be accessed as other types.
                        //This is also to be consistent for optimized functions that compresses the stack.
                    }

                    newStack.MetaData = new StackMetaData
                    {
                        Func = proto,
                        SigDesc = SignatureDesc.Empty,
                        SigNumberOffset = proto.SigRegionOffsetV,
                        SigObjectOffset = proto.SigRegionOffsetO,
                        VarargStart = varargStart,
                        VarargType = VMSpecializationType.Polymorphic,
                        VarargLength = varargLength,
                    };

                    //Before entering the loop, clear the current sig.
                    stack.ClearSigBlock();

                    InterpreterLoop(0, thread, ref newStack);

                    //Adjust return values (without moving additional to vararg list).
                    if (!stack.TryAdjustSigBlockRight(ref stack.MetaData.Func.SigDesc[c], null, out _))
                    {
                        JumpToFallback(thread, ref stack);
                        break;
                    }

                    //Clear object stack to avoid memory leak.
                    if (newStack.Head == stack.Head)
                    {
                        //Shared.
                        var clearStart = stack.MetaData.SigTotalOLength;
                        newStack.ObjectFrame.Slice(clearStart).Clear();
                    }
                    else
                    {
                        newStack.ObjectFrame.Clear();
                    }

                    thread.Stack.Deallocate(ref newStack);
                }
                else
                {
                    //native closure/native func
                    throw new NotImplementedException();
                }

                if ((Opcodes)(ii >> 24) == Opcodes.CALLC)
                {
                    stack.ClearSigBlock();
                }
                break;
            }
            case Opcodes.VARG:
            {
                //Currently only support unspecialized stack.
                Debug.Assert(lastWriteO == lastWriteV);
                Debug.Assert(stack.MetaData.SigNumberOffset == stack.MetaData.SigObjectOffset);
                stack.AppendVarargSig(stack.MetaData.VarargType, stack.MetaData.VarargLength);
                var stackStart = stack.MetaData.SigNumberOffset;
                for (int i = 0; i < stack.MetaData.VarargLength; ++i)
                {
                    stack.SetU(i + stackStart, thread.VarargStack[i + stack.MetaData.VarargStart]);
                }
                lastWriteO = lastWriteV = stackStart + stack.MetaData.VarargLength;
                break;
            }
            case Opcodes.SIG:
            {
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                int c = (int)(ii & 0xFF);

                stack.SetSigBlock(ref stack.MetaData.Func.SigDesc[a], b, c);
                break;
            }
            }
        }

        //ret: set sig for parent frame
        [InlineSwitchCase]
        private static void Ret(Thread thread, ref StackFrame stack,
            ref int pc, uint ii, ref int lastWriteO, ref int lastWriteV, out bool ret)
        {
            ret = true;
            switch ((Opcodes)(ii >> 24))
            {
            case Opcodes.RET0:
            {
                //No need to pass anything to caller.
                return;
            }
            case Opcodes.RETN:
            {
                //First execute SIG.
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                int c = (int)(ii & 0xFF);

                stack.SetSigBlock(ref stack.MetaData.Func.SigDesc[a], b, c);

                //Then return.
                Span<object> retObj;
                Span<double> retNum;
                if (stack.Last.Length == 0)
                {
                    //This is the first frame. Simply copy to the start of this frame.
                    retObj = stack.ObjectFrame;
                    retNum = stack.NumberFrame;
                }
                else
                {
                    unsafe
                    {
                        ref var lastStack = ref StackFrame.GetLast(ref stack.Last[0]);
                        lastStack.ReplaceSigBlock(ref stack.MetaData);

                        if (stack.Last[0] == stack.Head)
                        {
                            //The last one shares the same stack segment. Copy to this frame's start.
                            retObj = stack.ObjectFrame;
                            retNum = stack.NumberFrame;
                        }
                        else
                        {
                            //The last one is on a previous stack. Find the location using last frame's data.
                            retObj = lastStack.ObjectFrame[lastStack.MetaData.SigObjectOffset..];
                            retNum = lastStack.NumberFrame[lastStack.MetaData.SigNumberOffset..];
                        }
                    }
                }
                var ol = stack.MetaData.SigTotalOLength;
                var vl = stack.MetaData.SigTotalVLength;
                for (int i = 0; i < ol; ++i)
                {
                    retObj[i] = stack.ObjectFrame[stack.MetaData.SigObjectOffset + i];
                }
                for (int i = 0; i < vl; ++i)
                {
                    retNum[i] = stack.NumberFrame[stack.MetaData.SigNumberOffset + i];
                }
                return;
            }
            }
            ret = false;
        }
    }
}
