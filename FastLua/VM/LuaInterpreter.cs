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

        internal static void Execute(Thread thread, LClosure closure, ref StackFrame stack)
        {
            InterpreterLoop(0, thread, closure, ref stack);
        }

        private static partial void InterpreterLoop(int startPc, Thread thread, LClosure closure, ref StackFrame lastFrame);

        [InlineSwitch(typeof(LuaInterpreter))]
        private static void InterpreterLoop_Template(int startPc, Thread thread, LClosure closure, ref StackFrame lastFrame)
        {
            //This allocates the new frame's stack, which may overlap with the current (to pass args)
            //TODO seems a lot of work here in Allocate. Should try to simplify.
            //Especially the sig check.
            var proto = closure.Proto;
            var stack = thread.Stack.Allocate(thread, ref lastFrame, proto.NumStackSize, proto.ObjStackSize,
                onSameSeg: thread.SigDesc.SigType.Vararg.HasValue);

            //Adjust argument list according to the requirement of the callee.
            //Also remove vararg into separate stack.
            int varargStart = thread.VarargTotalLength;
            if (!thread.TryAdjustSigBlockRight(ref stack, ref proto.ParameterSig, thread.VarargStack, out var varargLength))
            {
                //Cannot adjust argument list. Adjust the argument list to unspecialized form and call fallback.
                thread.SigDesc.SigType.AdjustStackToUnspecialized();
                JumpToFallback(thread, ref stack);
                return;
            }

            //Push closure's upvals.
            Debug.Assert(closure.UpvalLists.Length <= proto.LocalRegionOffsetO - proto.UpvalRegionOffset);
            for (int i = 0; i < closure.UpvalLists.Length; ++i)
            {
                stack.ObjectFrame[proto.UpvalRegionOffset + i] = closure.UpvalLists[i];
                //We could also set types in value frame, but this region should never be accessed as other types.
                //This is also to be consistent for optimized functions that compresses the stack.
            }

            stack.MetaData = new StackMetaData
            {
                Func = proto,
                VarargStart = varargStart,
                VarargType = VMSpecializationType.Polymorphic,
                VarargLength = varargLength,
            };
            thread.ClearSigBlock();
            thread.SigNumberOffset = proto.SigRegionOffsetV;
            thread.SigObjectOffset = proto.SigRegionOffsetO;

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
                    if (ret) goto loopEnd;
                    break;
                }
            }
        loopEnd:
            //Clear object stack to avoid memory leak.
            if (stack.Head == lastFrame.Head)
            {
                //Shared.
                var clearStart = thread.SigTotalOLength;
                stack.ObjectFrame.Slice(clearStart).Clear();
            }
            else
            {
                stack.ObjectFrame.Clear();
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
            case Opcodes.ADD_D:
            {
                var a = (byte)(ii >> 16);
                var b = (byte)(ii >> 8);
                var c = (byte)ii;
                stack.NumberFrame[a] = stack.NumberFrame[b] + stack.NumberFrame[c];
                //lastWriteO = lastWriteV = a; //~5% overhead
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
                int u = thread.SigObjectOffset;
                int n = thread.SigVOLength;
                var nclosure = new LClosure
                {
                    Proto = stack.MetaData.Func.ChildFunctions[b],
                    UpvalLists = new TypedValue[n][],
                };
                var src = MemoryMarshal.CreateSpan(ref Unsafe.As<object, TypedValue[]>(ref stack.ObjectFrame[u]), n);
                src.CopyTo(nclosure.UpvalLists.AsSpan());
                stack.SetU(a, TypedValue.MakeLClosure(nclosure));
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
                thread.ResizeSigBlockLeft(ref argSig, lastWriteO + 1 - argSig.SigFOLength, lastWriteV + 1 - argSig.SigFVLength);

                var newFuncP = stack.ObjectFrame[a];
                if (newFuncP is LClosure lc)
                {
                    InterpreterLoop(0, thread, lc, ref stack);

                    //Adjust return values (without moving additional to vararg list).
                    if (!thread.TryAdjustSigBlockRight(ref stack, ref stack.MetaData.Func.SigDesc[c], null, out _))
                    {
                        JumpToFallback(thread, ref stack);
                        break;
                    }
                }
                else
                {
                    //native closure/native func
                    throw new NotImplementedException();
                }

                if ((Opcodes)(ii >> 24) == Opcodes.CALLC)
                {
                    thread.ClearSigBlock();
                }
                break;
            }
            case Opcodes.VARG:
            {
                //Currently only support unspecialized stack.
                Debug.Assert(lastWriteO == lastWriteV);
                Debug.Assert(thread.SigNumberOffset == thread.SigObjectOffset);
                thread.AppendVarargSig(stack.MetaData.VarargType, stack.MetaData.VarargLength);
                var stackStart = thread.SigNumberOffset;
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

                thread.SetSigBlock(ref stack.MetaData.Func.SigDesc[a], b, c);
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
                goto loopEnd;
            }
            case Opcodes.RETN:
            {
                //First execute SIG.
                int a = (int)((ii >> 16) & 0xFF);
                int b = (int)((ii >> 8) & 0xFF);
                int c = (int)(ii & 0xFF);

                thread.SetSigBlock(ref stack.MetaData.Func.SigDesc[a], b, c);

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
                        //lastStack.ReplaceSigBlock(ref stack.MetaData);

                        if (stack.Last[0] == stack.Head)
                        {
                            //The last one shares the same stack segment. Copy to this frame's start.
                            retObj = stack.ObjectFrame;
                            retNum = stack.NumberFrame;
                        }
                        else
                        {
                            //The last one is on a previous stack. Find the location using last frame's data.
                            retObj = lastStack.ObjectFrame[thread.SigObjectOffset..];
                            retNum = lastStack.NumberFrame[thread.SigNumberOffset..];
                        }
                    }
                }
                var ol = thread.SigTotalOLength;
                var vl = thread.SigTotalVLength;
                for (int i = 0; i < ol; ++i)
                {
                    retObj[i] = stack.ObjectFrame[thread.SigObjectOffset + i];
                }
                for (int i = 0; i < vl; ++i)
                {
                    retNum[i] = stack.NumberFrame[thread.SigNumberOffset + i];
                }
                goto loopEnd;
            }
            }
            ret = false;
        loopEnd:;
        }
    }
}
