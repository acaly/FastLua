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
    public partial class LuaInterpreter
    {
        public static int Execute(Thread thread, LClosure closure, AsyncStackInfo stackInfo, int argOffset, int argSize)
        {
            //C#-Lua boundary: before.

            ref var stack = ref thread.GetFrame(stackInfo.StackFrame);
            var values = thread.GetFrameValues(ref stack);

            //Set up argument type info for Lua function.
            var sig = new StackSignatureState(argOffset, argSize);
            try
            {
                //For simplicity, C#-Lua call always passes arguments in same segment.
                InterpreterLoop(thread, closure, ref stack, ref sig, forceOnSameSeg: true);
            }
            catch (RecoverableException)
            {
                //TODO recover
                throw new NotImplementedException();
            }
            catch (OverflowException)
            {
                //TODO check whether the last frame is Lua frame and currently executing
                //an int instruction. If so, deoptimize the function and recover.
                throw;
            }
            catch
            {
                //TODO unrolled Lua frames (deallocate, clear values).
                throw;
            }

            //C#-Lua boundary: after.

            if (sig.Type is not null)
            {
                var adjusted = sig.AdjustRight(in values, StackSignature.EmptyV);
                Debug.Assert(adjusted);
                return sig.VLength;
            }
            else
            {
                return 0;
            }
        }

        private static void CallNonLuaFunction(Thread thread, Span<StackFrame> stack, ref StackFrameValues values,
            ref StackSignatureState sig, object func, ref int pc)
        {
            int retSize;
            if (func is NativeFunctionDelegate nativeFunc)
            {
                //Lua-C# boundary, before.

                //Allocate C# frame.
                //This frame is not too different from a Lua frame.
                //Leave PC = 0 (used by RET0/RETN to check whether last frame is Lua frame).
                var nativeFrame = thread.AllocateNextFrame(ref stack[0], in sig,
                    Options.DefaultNativeStackSize, onSameSeg: true);

                //Native function always accepts unspecialized stack.
                var adjust = sig.AdjustRight(in values, StackSignature.EmptyV);
                Debug.Assert(adjust);

                //Call native function.
                retSize = nativeFunc(new(thread.ConvertToNativeFrame(ref nativeFrame[0])), sig.VLength);
                thread.DeallocateFrame(ref nativeFrame);
            }
            else if (func is AsyncNativeFunctionDelegate asyncNativeFunc)
            {
                //Lua-C# boundary, before.

                //Allocate C# frame.
                //This frame is not too different from a Lua frame.
                //Leave PC = 0 (used by RET0/RETN to check whether last frame is Lua frame).
                var nativeFrame = thread.AllocateNextFrame(ref stack[0], in sig,
                    Options.DefaultNativeStackSize, onSameSeg: true);

                //Native function always accepts unspecialized stack.
                var adjust = sig.AdjustRight(in values, StackSignature.EmptyV);
                Debug.Assert(adjust);

                //Call native function.
                var retTask = asyncNativeFunc(thread.ConvertToNativeFrame(ref nativeFrame[0]), sig.VLength);
                if (!retTask.IsCompleted)
                {
                    //Yield (need to save the task).
                    throw new NotImplementedException();
                }

                retSize = retTask.Result;
                thread.DeallocateFrame(ref nativeFrame);
            }
            else
            {
                //TODO check metatable
                throw new NotImplementedException();
            }

            //After native function call returns.

            //Lua-C# boundary, after.

            //Set sig block (this will be adjusted later).
            sig.SetUnspecializedVararg(sig.Offset, retSize);

            //Check Lua stack relocation.
            if (!Unsafe.AreSame(ref values[0], ref thread.GetFrameValues(ref stack[0]).Span[0]))
            {
                pc += 1;
                stack[0].PC = pc;

                //TODO Need to set up some flags so that when recovered we start from ret sig adjustment.
                throw new RecoverableException();
            }
        }

        private static void InterpreterLoop(Thread thread, LClosure closure, ref StackFrame lastFrame,
            ref StackSignatureState parentSig, bool forceOnSameSeg)
        {
            //TODO recover execution
            //TODO in recovery, onSameSeg should be set depending on whether lastFrame's Segment
            //equals this frame's Segment.

            var proto = closure.Proto;
            var stack = thread.AllocateNextFrame(ref lastFrame, in parentSig, proto.StackSize, forceOnSameSeg);

            var sig = parentSig;
            sig.Offset = 0; //Args are at the beginning in the new frame.

        enterNewLuaFrame:
            var values = thread.GetFrameValues(ref stack[0]);

            //Adjust argument list according to the requirement of the callee.
            //Also remove vararg into separate stack.
            if (!sig.AdjustRight(in values, proto.ParameterSig))
            {
                throw new NotImplementedException();
            }
            sig.MoveVararg(in values, thread.VarargStack, ref stack[0].VarargInfo);
            sig.Clear();

            //Push closure's upvals.
            Debug.Assert(closure.UpvalLists.Length <= proto.LocalRegionOffset - proto.UpvalRegionOffset);
            for (int i = 0; i < closure.UpvalLists.Length; ++i)
            {
                values[proto.UpvalRegionOffset + i].Object = closure.UpvalLists[i];
                //We could also set types in value frame, but this region should never be accessed as other types.
                //This is also to be consistent for optimized functions that compresses the stack.
            }

            var pc = 0;
            var lastWrite = 0;
            var inst = proto.Instructions;
            uint ii;

        continueOldFrame:
            while (true)
            {
                ii = inst[pc++];
                switch ((OpCodes)(ii >> 24))
                {
                case OpCodes.NOP:
                    break;
                case OpCodes.ISLT:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    if (CompareValue(values[a], values[b]) < 0)
                    {
                        pc += (sbyte)(byte)(ii & 0xFF);
                    }
                    break;
                }
                case OpCodes.ISLE:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    if (CompareValue(values[a], values[b]) <= 0)
                    {
                        pc += (sbyte)(byte)(ii & 0xFF);
                    }
                    break;
                }
                case OpCodes.ISNLT:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    if (!(CompareValue(values[a], values[b]) < 0))
                    {
                        pc += (sbyte)(byte)(ii & 0xFF);
                    }
                    break;
                }
                case OpCodes.ISNLE:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    if (!(CompareValue(values[a], values[b]) <= 0))
                    {
                        pc += (sbyte)(byte)(ii & 0xFF);
                    }
                    break;
                }
                case OpCodes.ISEQ:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    if (CompareValue(values[a], values[b]) == 0)
                    {
                        pc += (sbyte)(byte)(ii & 0xFF);
                    }
                    break;
                }
                case OpCodes.ISNE:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    //!(cmp == 0) will return true for NaN comparison.
                    if (!(CompareValue(values[a], values[b]) == 0))
                    {
                        pc += (sbyte)(byte)(ii & 0xFF);
                    }
                    break;
                }
                case OpCodes.ISTC:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    if (values[b].ToBoolVal())
                    {
                        values[a] = values[b];
                        pc += (sbyte)(byte)(ii & 0xFF);
                    }
                    lastWrite = a;
                    break;
                }
                case OpCodes.ISFC:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    if (!values[b].ToBoolVal())
                    {
                        values[a] = values[b];
                        pc += (sbyte)(byte)(ii & 0xFF);
                    }
                    lastWrite = a;
                    break;
                }
                case OpCodes.MOV:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    values[a] = values[b];
                    lastWrite = a;
                    break;
                }
                case OpCodes.NOT:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    values[a] = values[b].ToBoolVal() ? TypedValue.False : TypedValue.True;
                    lastWrite = a;
                    break;
                }
                case OpCodes.NEG:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    switch (values[b].Type)
                    {
                    case VMSpecializationType.Int:
                        values[a] = TypedValue.MakeInt(-values[b].IntVal);
                        break;
                    case VMSpecializationType.Double:
                        values[a] = TypedValue.MakeDouble(-values[b].DoubleVal);
                        break;
                    default:
                        values[a] = UnaryNeg(values[b]);
                        break;
                    }
                    lastWrite = a;
                    break;
                }
                case OpCodes.LEN:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    values[a] = UnaryLen(values[b]);
                    lastWrite = a;
                    break;
                }
                case OpCodes.ADD:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    values[a] = Add(values[b], values[c]);
                    lastWrite = a;
                    break;
                }
                case OpCodes.ADD_D:
                {
                    var a = (byte)(ii >> 16);
                    var b = (byte)(ii >> 8);
                    var c = (byte)ii;
                    values[a].Number = values[b].Number + values[c].Number;
                    lastWrite = a; //+<5% overhead
                    break;
                }
                case OpCodes.SUB:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    values[a] = Sub(values[b], values[c]);
                    lastWrite = a;
                    break;
                }
                case OpCodes.MUL:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    values[a] = Mul(values[b], values[c]);
                    lastWrite = a;
                    break;
                }
                case OpCodes.DIV:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    values[a] = Div(values[b], values[c]);
                    lastWrite = a;
                    break;
                }
                case OpCodes.MOD:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    values[a] = Mod(values[b], values[c]);
                    lastWrite = a;
                    break;
                }
                case OpCodes.POW:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    values[a] = Pow(values[b], values[c]);
                    lastWrite = a;
                    break;
                }
                case OpCodes.CAT:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);

                    var sb = thread.StringBuilder;
                    sb.Clear();
                    for (int i = b; i < c; ++i)
                    {
                        WriteString(sb, values[i]);
                    }
                    values[a] = TypedValue.MakeString(sb.ToString());
                    lastWrite = a;
                    break;
                }
                case OpCodes.K:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    values[a] = proto.Constants[b];
                    lastWrite = a;
                    break;
                }
                case OpCodes.K_D:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    values[a].Number = proto.Constants[b].Number;
                    lastWrite = a;
                    break;
                }
                case OpCodes.UGET:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    values[a] = values.GetUpvalue(b, c);
                    lastWrite = a;
                    break;
                }
                case OpCodes.USET:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    values.GetUpvalue(b, c) = values[a];
                    break;
                }
                case OpCodes.UNEW:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    values[a].Object = new TypedValue[b];
                    //Type not set.
                    break;
                }
                case OpCodes.FNEW:
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
                        nclosure.UpvalLists[i] = (TypedValue[])values[upvalLists[i]].Object;
                    }
                    values[a] = TypedValue.MakeLClosure(nclosure);
                    lastWrite = a;
                    break;
                }
                case OpCodes.TNEW:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    values[a] = TypedValue.MakeTable(new Table());
                    lastWrite = a;
                    break;
                }
                case OpCodes.TGET:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    if (values[b].Object is Table t)
                    {
                        t.Get(values[c], out values[a]);
                    }
                    else
                    {
                        GetTable(ref values[b], ref values[c], ref values[a]);
                    }
                    lastWrite = a;
                    break;
                }
                case OpCodes.TSET:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    if (values[b].Object is Table t)
                    {
                        t.Set(values[c], values[a]);
                    }
                    else
                    {
                        SetTable(ref values[b], ref values[c], ref values[a]);
                    }
                    break;
                }
                case OpCodes.TINIT:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    var t = (Table)values[a].Object;
                    var initSig = proto.SigTypes[c];
                    //TODO should convert (cannot add initSig.FLength with sig.VLength)
                    //TODO check whether we should use sig.Offset instead of b
                    var span = values.Span.Slice(b, initSig.FLength + sig.VLength);
                    t.SetSequence(span, initSig);
                    sig.Clear();
                    break;
                }
                case OpCodes.CALL:
                case OpCodes.CALLC:
                {
                    //Similar logic is also used in FORG. Be consistent whenever CALL/CALLC is updated.

                    stack[0].PC = pc;

                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);

                    //Adjust current sig block at the left side.
                    //This allows to merge other arguments that have already been pushed before.
                    //If current sig is empty, set the starting point based on lastWrite.
                    var argSig = proto.SigTypes[b];
                    var sigStart = Math.Max(lastWrite + 1 - argSig.FLength, proto.SigRegionOffset);
                    sigStart = sig.AdjustLeft(in values, argSig, sigStart);

                    var newFuncP = values[a].Object;
                    if (newFuncP is LClosure lc)
                    {
                        stack[0].RetSigIndex = c;

                        //Save state.
                        stack[0].LastWrite = lastWrite;
                        stack[0].SigOffset = sigStart;
                        thread.ClosureStack.Push(closure);

                        //Setup proto, closure, and stack.
                        closure = lc;
                        proto = lc.Proto;
                        //TODO is argSig.HasV necessary here?
                        stack = thread.AllocateNextFrame(ref stack[0], in sig, proto.StackSize,
                            argSig.Vararg.HasValue || proto.SigTypes[c].Vararg.HasValue);

                        goto enterNewLuaFrame;
                    }
                    else
                    {
                        CallNonLuaFunction(thread, stack, ref values, ref sig, newFuncP, ref pc);
                        goto returnFromNativeFunction;
                    }
                }
                case OpCodes.VARG:
                case OpCodes.VARGC:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);

                    //TODO check whether this will write out of range
                    //Also check lastWrite below.
                    for (int i = 0; i < stack[0].VarargInfo.VarargLength; ++i)
                    {
                        values[b + i] = thread.VarargStack[stack[0].VarargInfo.VarargStart + i];
                    }

                    //Overwrite sig as a vararg.
                    sig.Type = proto.VarargSig;
                    sig.Offset = b;
                    sig.VLength = stack[0].VarargInfo.VarargLength;

                    //Then adjust to requested (this is needed in assignment statement).
                    var adjustmentSuccess = sig.AdjustRight(in values, proto.SigTypes[a]);
                    Debug.Assert(adjustmentSuccess);
                    if (!proto.SigTypes[a].Vararg.HasValue)
                    {
                        sig.DiscardVararg(in values);
                    }

                    if ((OpCodes)(ii >> 24) == OpCodes.VARGC)
                    {
                        sig.Clear();
                        lastWrite = 0;
                    }
                    else
                    {
                        lastWrite = b + stack[0].VarargInfo.VarargLength - 1;
                    }
                    break;
                }
                case OpCodes.VARG1:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    if (stack[0].VarargInfo.VarargLength == 0)
                    {
                        values[a] = TypedValue.Nil;
                    }
                    else
                    {
                        values[a] = thread.VarargStack[stack[0].VarargInfo.VarargStart];
                    }
                    break;
                }
                case OpCodes.JMP:
                {
                    pc += (short)(ushort)(ii & 0xFFFF);
                    break;
                }
                case OpCodes.FORI:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    ForceConvDouble(ref values[a]);
                    ForceConvDouble(ref values[a + 1]);
                    ForceConvDouble(ref values[a + 2]);
                    //Treat the values as double.
                    ref var n1 = ref values[a].Number;
                    ref var n2 = ref values[a + 1].Number;
                    ref var n3 = ref values[a + 2].Number;
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
                case OpCodes.FORL:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    //Treat the values as double.
                    ref var n1 = ref values[a].Number;
                    ref var n2 = ref values[a + 1].Number;
                    ref var n3 = ref values[a + 2].Number;
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
                case OpCodes.FORG:
                {
                    //Similar logic is also used in CALL/CALLC. Be consistent whenever this is updated.

                    stack[0].PC = pc;

                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);

                    //FORG always call with Polymorphic_2 arguments.
                    values[a + 3] = values[a + 1]; //s
                    values[a + 4] = values[a + 2]; //var
                    //TODO we changed arg passing to in, so we don't need a copy in proto.SigDesc list.
                    //Directly reference sig desc.

                    sig.Type = StackSignature.Polymorphic_2;
                    sig.Offset = a + 3;
                    sig.VLength = 0;

                    var sigStart = sig.Offset; //a + 3

                    var newFuncP = values[a].Object;
                    if (newFuncP is LClosure lc)
                    {
                        stack[0].RetSigIndex = b;

                        //Save state.
                        stack[0].LastWrite = lastWrite;
                        stack[0].SigOffset = sigStart;
                        thread.ClosureStack.Push(closure);

                        //Setup proto, closure, and stack.
                        closure = lc;
                        proto = lc.Proto;
                        stack = thread.AllocateNextFrame(ref stack[0], in sig, proto.StackSize, onSameSeg: false);

                        goto enterNewLuaFrame;
                    }
                    else
                    {
                        CallNonLuaFunction(thread, stack, ref values, ref sig, newFuncP, ref pc);
                        goto returnFromNativeFunction;
                    }
                }
                case OpCodes.RET0:
                {
                    //No need to pass anything to caller.
                    sig.Type = StackSignature.Empty;
                    goto returnFromLuaFunction;
                }
                case OpCodes.RETN:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);

                    //First adjust sig block.
                    //TODO this should be changed to adjust left, similar to call
                    sig.Type = proto.SigTypes[a];
                    sig.Offset = b;
                    //Keep vlength.

                    //Then return.
                    Span<TypedValue> retSpan;
                    var l = sig.TotalLength;

                    if (stack[0].ActualOnSameSegment)
                    {
                        //The last one shares the same stack segment. Copy to this frame's start.
                        retSpan = values.Span;
                    }
                    else
                    {
                        //The last one is on a previous stack. Find the location using last frame's data.
                        ref var lastFrameRef = ref thread.GetLastFrame(ref stack[0]);
                        retSpan = thread.GetFrameValues(ref lastFrameRef).Span[lastFrameRef.SigOffset..];
                        l = Math.Min(l, retSpan.Length);
                    }
                    for (int i = 0; i < l; ++i)
                    {
                        //The index operator used below has no range check, because we may copy from the
                        //range outside this current frame.
                        //The onSameSegment mechanism ensures this range is valid.
                        retSpan[i] = values[sig.Offset + i];
                    }
                    goto returnFromLuaFunction;
                }
                }
            }

        returnFromLuaFunction:
            //Jump target when exiting a Lua frame (to either another Lua frame or a native frame).

            //Clear object stack to avoid memory leak.
            if (stack[0].ActualOnSameSegment)
            {
                //Shared.
                var clearStart = sig.TotalLength;
                values.Span[clearStart..].Clear();
            }
            else
            {
                values.Span.Clear();
            }
            thread.DeallocateFrame(ref stack);

            pc = stack[0].PC;
            if (pc == 0)
            {
                //Lua frames cannot have pc == 0 when calling another function.
                //This is a native (C#) frame. Exit interpreter.
                parentSig.Type = sig.Type;
                parentSig.VLength = sig.VLength;
                return;
            }

            //Exit from one Lua frame to another Lua frame.
            //Restore parent frame state and continue.

            //Restore.
            closure = thread.ClosureStack.Pop();
            proto = closure.Proto;
            values = thread.GetFrameValues(ref stack[0]);
            sig.Offset = stack[0].SigOffset;
            inst = proto.Instructions;
            lastWrite = stack[0].LastWrite;

        returnFromNativeFunction:
            //Jump target from calls to native (C#) functions. These calls also need to adjust the
            //sig block, but it does not require the same restore step.

            //Adjust return values (without moving additional to vararg list).
            if (!sig.AdjustRight(in values, proto.SigTypes[stack[0].RetSigIndex]))
            {
                throw new NotImplementedException();
            }
            if (!proto.SigTypes[stack[0].RetSigIndex].Vararg.HasValue)
            {
                sig.DiscardVararg(in values);
            }

            ii = proto.Instructions[pc - 1];
            switch ((OpCodes)(ii >> 24))
            {
            case OpCodes.CALLC:
                sig.Clear();
                lastWrite = 0;
                break;
            case OpCodes.FORG:
                sig.Clear();
                lastWrite = 0;

                //For-loop related logic.
                int a = (int)((ii >> 16) & 0xFF);

                if (values[a + 3].Type == VMSpecializationType.Nil)
                {
                    pc += (sbyte)(byte)(ii & 0xFF);
                }
                values[a + 2] = values[a + 3]; //var = var_1
                break;
            }
            goto continueOldFrame;
        }
    }
}
