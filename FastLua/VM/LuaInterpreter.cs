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

        private static void InterpreterLoop(int startPc, Thread thread, LClosure closure, ref StackFrame lastFrame)
        {
            //This allocates the new frame's stack, which may overlap with the current (to pass args)
            //TODO seems a lot of work here in Allocate. Should try to simplify.
            //Especially the sig check.
            var proto = closure.Proto;
            var stack = thread.Stack.Allocate(thread, ref lastFrame, proto.StackSize,
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
            Debug.Assert(closure.UpvalLists.Length <= proto.LocalRegionOffset - proto.UpvalRegionOffset);
            for (int i = 0; i < closure.UpvalLists.Length; ++i)
            {
                stack.ValueFrame[proto.UpvalRegionOffset + i].Object = closure.UpvalLists[i];
                //We could also set types in value frame, but this region should never be accessed as other types.
                //This is also to be consistent for optimized functions that compresses the stack.
            }

            stack.MetaData = new StackMetaData
            {
                VarargStart = varargStart,
                VarargType = VMSpecializationType.Polymorphic,
                VarargLength = varargLength,
                OnSameSegment = stack.MetaData.OnSameSegment,
            };
            thread.ClearSigBlock();
            thread.SigOffset = proto.SigRegionOffset;

            var inst = proto.Instructions;
            var pc = startPc;
            int lastWriteO = 0, lastWriteV = 0;
            while (true)
            {
                var ii = inst[pc++];
                switch ((Opcodes)(ii >> 24))
                {
                case Opcodes.NOP:
                    break;
                case Opcodes.ISLT:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    if (CompareValue(stack.ValueFrame[a], stack.ValueFrame[b]) < 0)
                    {
                        pc += (sbyte)(byte)(ii & 0xFF);
                    }
                    break;
                }
                case Opcodes.ISLE:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    if (CompareValue(stack.ValueFrame[a], stack.ValueFrame[b]) <= 0)
                    {
                        pc += (sbyte)(byte)(ii & 0xFF);
                    }
                    break;
                }
                case Opcodes.ISNLT:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    if (!(CompareValue(stack.ValueFrame[a], stack.ValueFrame[b]) < 0))
                    {
                        pc += (sbyte)(byte)(ii & 0xFF);
                    }
                    break;
                }
                case Opcodes.ISNLE:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    if (!(CompareValue(stack.ValueFrame[a], stack.ValueFrame[b]) <= 0))
                    {
                        pc += (sbyte)(byte)(ii & 0xFF);
                    }
                    break;
                }
                case Opcodes.ISEQ:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    if (CompareValue(stack.ValueFrame[a], stack.ValueFrame[b]) == 0)
                    {
                        pc += (sbyte)(byte)(ii & 0xFF);
                    }
                    break;
                }
                case Opcodes.ISNE:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    //!(cmp == 0) will return true for NaN comparison.
                    if (!(CompareValue(stack.ValueFrame[a], stack.ValueFrame[b]) == 0))
                    {
                        pc += (sbyte)(byte)(ii & 0xFF);
                    }
                    break;
                }
                case Opcodes.ISTC:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    if (stack.ToBoolU(b))
                    {
                        stack.ValueFrame[a] = stack.ValueFrame[b];
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
                        stack.ValueFrame[a] = stack.ValueFrame[b];
                        pc += (sbyte)(byte)(ii & 0xFF);
                    }
                    lastWriteO = lastWriteV = a;
                    break;
                }
                case Opcodes.MOV:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    stack.ValueFrame[a] = stack.ValueFrame[b];
                    lastWriteO = lastWriteV = a;
                    break;
                }
                case Opcodes.NOT:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    stack.ValueFrame[a] = stack.ToBoolU(b) ? TypedValue.False : TypedValue.True;
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
                        stack.ValueFrame[a] = TypedValue.MakeInt(-stack.GetIntU(b));
                        break;
                    case VMSpecializationType.Double:
                        stack.ValueFrame[a] = TypedValue.MakeDouble(-stack.GetDoubleU(b));
                        break;
                    default:
                        stack.ValueFrame[a] = UnaryNeg(stack.ValueFrame[b]);
                        break;
                    }
                    lastWriteO = lastWriteV = a;
                    break;
                }
                case Opcodes.LEN:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    stack.ValueFrame[a] = UnaryLen(stack.ValueFrame[b]);
                    lastWriteO = lastWriteV = a;
                    break;
                }
                case Opcodes.ADD:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    stack.ValueFrame[a] = Add(stack.ValueFrame[b], stack.ValueFrame[c]);
                    lastWriteO = lastWriteV = a;
                    break;
                }
                case Opcodes.ADD_D:
                {
                    var a = (byte)(ii >> 16);
                    var b = (byte)(ii >> 8);
                    var c = (byte)ii;
                    stack.ValueFrame[a].Number = stack.ValueFrame[b].Number + stack.ValueFrame[c].Number;
                    //lastWriteO = lastWriteV = a; //+~5% overhead

                    //5-10% faster.
                    //Unsafe.Add(ref MemoryMarshal.GetReference(stack.NumberFrame), a) =
                    //    Unsafe.Add(ref MemoryMarshal.GetReference(stack.NumberFrame), b) +
                    //    Unsafe.Add(ref MemoryMarshal.GetReference(stack.NumberFrame), c);
                    break;
                }
                case Opcodes.SUB:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    stack.ValueFrame[a] = Sub(stack.ValueFrame[b], stack.ValueFrame[c]);
                    lastWriteO = lastWriteV = a;
                    break;
                }
                case Opcodes.MUL:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    stack.ValueFrame[a] = Mul(stack.ValueFrame[b], stack.ValueFrame[c]);
                    lastWriteO = lastWriteV = a;
                    break;
                }
                case Opcodes.DIV:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    stack.ValueFrame[a] = Div(stack.ValueFrame[b], stack.ValueFrame[c]);
                    lastWriteO = lastWriteV = a;
                    break;
                }
                case Opcodes.MOD:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    stack.ValueFrame[a] = Mod(stack.ValueFrame[b], stack.ValueFrame[c]);
                    lastWriteO = lastWriteV = a;
                    break;
                }
                case Opcodes.POW:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    stack.ValueFrame[a] = Pow(stack.ValueFrame[b], stack.ValueFrame[c]);
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
                        WriteString(sb, stack.ValueFrame[i]);
                    }
                    stack.ValueFrame[a] = TypedValue.MakeString(sb.ToString());
                    lastWriteO = lastWriteV = a;
                    break;
                }
                case Opcodes.K:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    stack.ValueFrame[a] = proto.Constants[b];
                    lastWriteO = lastWriteV = a;
                    break;
                }
                case Opcodes.K_D:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    stack.ValueFrame[a].Number = proto.Constants[b].Number;
                    lastWriteO = lastWriteV = a;
                    break;
                }
                case Opcodes.UGET:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    stack.ValueFrame[a] = stack.GetUpvalOU(b, c);
                    lastWriteO = lastWriteV = a;
                    break;
                }
                case Opcodes.USET:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    stack.SetUpvalOU(b, c, stack.ValueFrame[a]);
                    break;
                }
                case Opcodes.UNEW:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    stack.ValueFrame[a].Object = new TypedValue[b];
                    //Type not set.
                    break;
                }
                case Opcodes.FNEW:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);

                    var (closureProto, upvalLists) = proto.ChildFunctions[b];
                    var nclosure = new LClosure
                    {
                        Proto = closureProto,
                        UpvalLists = new TypedValue[upvalLists.Length][],
                    };
                    for (int i = 0; i < upvalLists.Length; ++i)
                    {
                        nclosure.UpvalLists[i] = (TypedValue[])stack.ValueFrame[upvalLists[i]].Object;
                    }
                    stack.ValueFrame[a] = TypedValue.MakeLClosure(nclosure);
                    lastWriteO = lastWriteV = a;
                    break;
                }
                case Opcodes.TNEW:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    stack.ValueFrame[a] = TypedValue.MakeTable(new Table());
                    lastWriteO = lastWriteV = a;
                    break;
                }
                case Opcodes.TGET:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    //TODO should support metatable
                    var t = (Table)stack.ValueFrame[b].Object;
                    stack.ValueFrame[a] = t.Get(stack.ValueFrame[c]);
                    lastWriteO = lastWriteV = a;
                    break;
                }
                case Opcodes.TSET:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    //TODO should support metatable
                    var t = (Table)stack.ValueFrame[b].Object;
                    t.Set(stack.ValueFrame[c], stack.ValueFrame[a]);
                    break;
                }
                case Opcodes.TINIT:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    var t = (Table)stack.ValueFrame[a].Object;
                    ref var sig = ref proto.SigDesc[c];
                    var span = stack.ValueFrame.Slice(b, sig.SigFLength + thread.SigVLength);
                    t.SetSequence(span, ref sig);
                    thread.ClearSigBlock();
                    break;
                }
                case Opcodes.CALL:
                case Opcodes.CALLC:
                {
                    //Similar logic is also used in FORG. Be consistent whenever CALL/CALLC is updated.

                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);

                    //Adjust current sig block at the left side.
                    //This allows to merge other arguments that have already been pushed before.
                    //If current sig is empty, set the starting point based on lastWrite.
                    ref var argSig = ref proto.SigDesc[b];
                    thread.ResizeSigBlockLeft(ref argSig, lastWriteO + 1 - argSig.SigFLength);

                    var newFuncP = stack.ValueFrame[a].Object;
                    if (newFuncP is LClosure lc)
                    {
                        InterpreterLoop(0, thread, lc, ref stack);

                        //Adjust return values (without moving additional to vararg list).
                        if (!thread.TryAdjustSigBlockRight(ref stack, ref proto.SigDesc[c], null, out _))
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
                case Opcodes.VARGC:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);

                    for (int i = 0; i < stack.MetaData.VarargLength; ++i)
                    {
                        stack.ValueFrame[i + b] = thread.VarargStack[i + stack.MetaData.VarargStart];
                    }

                    //Overwrite sig as a vararg.
                    thread.SetSigBlockVararg(ref proto.VarargSig, b, stack.MetaData.VarargLength);
                    //Then adjust to requested (this is needed in assignment statement).
                    var adjustmentSuccess = thread.TryAdjustSigBlockRight(ref stack,
                        ref proto.SigDesc[a], null, out _);
                    Debug.Assert(adjustmentSuccess);

                    lastWriteO = lastWriteV = b + stack.MetaData.VarargLength - 1;

                    if ((Opcodes)(ii >> 24) == Opcodes.VARGC)
                    {
                        thread.ClearSigBlock();
                    }
                    break;
                }
                case Opcodes.VARG1:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    if (stack.MetaData.VarargLength == 0)
                    {
                        stack.ValueFrame[a] = TypedValue.Nil;
                    }
                    else
                    {
                        stack.ValueFrame[a] = thread.VarargStack[stack.MetaData.VarargStart];
                    }
                    break;
                }
                case Opcodes.JMP:
                {
                    pc += (short)(ushort)(ii & 0xFFFF);
                    break;
                }
                case Opcodes.FORI:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    ForceConvDouble(ref stack.ValueFrame[a]);
                    ForceConvDouble(ref stack.ValueFrame[a + 1]);
                    ForceConvDouble(ref stack.ValueFrame[a + 2]);
                    //Treat the values as double.
                    ref var n1 = ref stack.ValueFrame[a].Number;
                    ref var n2 = ref stack.ValueFrame[a + 1].Number;
                    ref var n3 = ref stack.ValueFrame[a + 2].Number;
                    if (n3 > 0)
                    {
                        if (!(n1 <= n2))
                        {
                            pc += (short)(ushort)(ii & 0xFFFF);
                        }
                    }
                    else
                    {
                        if (!(n1 >= n2))
                        {
                            pc += (short)(ushort)(ii & 0xFFFF);
                        }
                    }
                    break;
                }
                case Opcodes.FORL:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    //Treat the values as double.
                    ref var n1 = ref stack.ValueFrame[a].Number;
                    ref var n2 = ref stack.ValueFrame[a + 1].Number;
                    ref var n3 = ref stack.ValueFrame[a + 2].Number;
                    n1 += n3;
                    if (n3 > 0)
                    {
                        if (n1 <= n2)
                        {
                            pc += (short)(ushort)(ii & 0xFFFF);
                        }
                    }
                    else
                    {
                        if (n1 >= n2)
                        {
                            pc += (short)(ushort)(ii & 0xFFFF);
                        }
                    }
                    break;
                }
                case Opcodes.FORG:
                {
                    //Similar logic is also used in CALL/CALLC. Be consistent whenever this is updated.

                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);

                    //FORG always call with Polymorphic_2 arguments.
                    stack.ValueFrame[a + 3] = stack.ValueFrame[a + 1]; //s
                    stack.ValueFrame[a + 4] = stack.ValueFrame[a + 2]; //var
                    thread.SetSigBlock(ref proto.SigDesc[(int)WellKnownStackSignature.Polymorphic_2], a + 3);

                    var newFuncP = stack.ValueFrame[a].Object;
                    if (newFuncP is LClosure lc)
                    {
                        InterpreterLoop(0, thread, lc, ref stack);

                        //Adjust return values (without moving additional to vararg list).
                        if (!thread.TryAdjustSigBlockRight(ref stack, ref proto.SigDesc[b], null, out _))
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
                    thread.ClearSigBlock();

                    //For-loop related logic.
                    if (stack.ValueFrame[a + 3].Type == VMSpecializationType.Nil)
                    {
                        pc += (sbyte)(byte)(ii & 0xFF);
                    }
                    stack.ValueFrame[a + 2] = stack.ValueFrame[a + 3]; //var = var_1

                    break;
                }
                case Opcodes.SIG:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);

                    thread.SetSigBlock(ref proto.SigDesc[a], b);
                    break;
                }
                case Opcodes.RET0:
                {
                    //No need to pass anything to caller.
                    thread.ClearSigBlock();
                    goto loopEnd;
                }
                case Opcodes.RETN:
                {
                    //First execute SIG.
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);

                    thread.SetSigBlock(ref proto.SigDesc[a], b);

                    //Then return.
                    Span<TypedValue> retSpan;
                    var l = thread.SigTotalLength;
                    if (stack.Last.Length == 0)
                    {
                        //This is the first frame. Simply copy to the start of this frame.
                        retSpan = stack.ValueFrame;
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
                                retSpan = stack.ValueFrame;
                            }
                            else
                            {
                                //The last one is on a previous stack. Find the location using last frame's data.
                                retSpan = lastStack.ValueFrame[thread.SigOffset..];
                                l = Math.Min(l, retSpan.Length);
                            }
                        }
                    }
                    for (int i = 0; i < l; ++i)
                    {
                        retSpan[i] = stack.ValueFrame[thread.SigOffset + i];
                    }
                    goto loopEnd;
                }
                }
            }
        loopEnd:
            //Clear object stack to avoid memory leak.
            if (stack.Head == lastFrame.Head)
            {
                //Shared.
                var clearStart = thread.SigTotalLength;
                stack.ValueFrame.Slice(clearStart).Clear();
            }
            else
            {
                stack.ValueFrame.Clear();
            }
        }

        private static void JumpToFallback(Thread thread, ref StackFrame stack)
        {
            throw new NotImplementedException();
        }
    }
}
