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
            var sig = new InterFrameSignatureState
            {
                Type = StackSignature.EmptyV,
                Offset = argOffset,
                VLength = argSize,
            };
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

            if (sig.Type == StackSignature.Empty)
            {
                return 0;
            }
            if (!sig.Type.IsUnspecialized)
            {
                sig.Type.AdjustStackToUnspecialized(in values);
                //TODO need some info from AdjustStackToUnspecialized
                throw new NotImplementedException();
            }
            return sig.VLength + sig.Type.FLength;
        }

        private static void InterpreterLoop(Thread thread, LClosure closure, ref StackFrame lastFrame,
            ref InterFrameSignatureState parentSig, bool forceOnSameSeg)
        {
            //TODO recover execution
            //TODO in recovery, onSameSeg should be set depending on whether lastFrame's Segment
            //equals this frame's Segment.

            var proto = closure.Proto;
            var stack = thread.AllocateNextFrame(ref lastFrame, parentSig.Offset, parentSig.TotalLength,
                proto.StackSize, forceOnSameSeg);

            //var sig = parentSig;
            //sig.Offset = 0; //Args are at the beginning in the new frame.

            StackInfo nextNativeStackInfo = default;
            nextNativeStackInfo.AsyncStackInfo = thread.ConvertToNativeFrame(ref stack[0]);

            StackFrameValues values = thread.GetFrameValues(ref stack[0]);
            StackSignatureState sig = new(in values, parentSig.Type, parentSig.VLength, proto.SigTypes, proto.ParameterSig);
            sig.MoveVararg(in values, thread.VarargStack, ref stack[0].VarargInfo);
            sig.Clear();

        enterNewLuaFrame:
            nextNativeStackInfo.AsyncStackInfo.StackFrame += 1;

            //Push closure's upvals.
            Debug.Assert(closure.UpvalLists.Length <= proto.LocalRegionOffset - proto.UpvalRegionOffset);
            for (int i = 0; i < closure.UpvalLists.Length; ++i)
            {
                values[proto.UpvalRegionOffset + i].Object = closure.UpvalLists[i];
                //We could also set types in value frame, but this region should never be accessed as other types.
                //This is also to be consistent for optimized functions that compresses the stack.
            }

            var pc = 0;
            var inst = proto.Instructions;
            uint ii;
            object nonLuaFunc;

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
                    break;
                }
                case OpCodes.MOV:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    values[a] = values[b];
                    break;
                }
                case OpCodes.NOT:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    values[a] = values[b].ToBoolVal() ? TypedValue.False : TypedValue.True;
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
                    break;
                }
                case OpCodes.LEN:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    values[a] = UnaryLen(values[b]);
                    break;
                }
                case OpCodes.ADD:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    values[a] = Add(values[b], values[c]);
                    break;
                }
                case OpCodes.ADD_D:
                {
                    var a = (byte)(ii >> 16);
                    var b = (byte)(ii >> 8);
                    var c = (byte)ii;
                    values[a].Number = values[b].Number + values[c].Number;
                    break;
                }
                case OpCodes.SUB:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    values[a] = Sub(values[b], values[c]);
                    break;
                }
                case OpCodes.MUL:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    values[a] = Mul(values[b], values[c]);
                    break;
                }
                case OpCodes.DIV:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    values[a] = Div(values[b], values[c]);
                    break;
                }
                case OpCodes.MOD:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    values[a] = Mod(values[b], values[c]);
                    break;
                }
                case OpCodes.POW:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    values[a] = Pow(values[b], values[c]);
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
                    break;
                }
                case OpCodes.K:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    values[a] = proto.Constants[b];
                    break;
                }
                case OpCodes.K_D:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    values[a].Number = proto.Constants[b].Number;
                    break;
                }
                case OpCodes.UGET:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    int b = (int)((ii >> 8) & 0xFF);
                    int c = (int)(ii & 0xFF);
                    values[a] = values.GetUpvalue(b, c);
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
                    break;
                }
                case OpCodes.TNEW:
                {
                    int a = (int)((ii >> 16) & 0xFF);
                    values[a] = TypedValue.MakeTable(new Table());
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
                    int l1 = (int)((ii >> 8) & 0xFF);
                    int l2 = (int)(ii & 0xFF);
                    if (l2 == 0)
                    {
                        sig.AdjustLeft(in values, proto.SigTypes, l1);
                    }
                    else
                    {
                        sig.TypeId = l1;
                        sig.FLength = proto.SigTypes[l1].FLength; //TODO can we eliminate this?
                        sig.Offset = l2;
                    }
                    var span = values.Span.Slice(sig.Offset, sig.TotalLength);
                    ((Table)values[a].Object).SetSequence(span, proto.SigTypes[sig.TypeId]);
                    sig.Clear();
                    break;
                }
                case OpCodes.CALL:
                case OpCodes.CALLC:
                {
                    //Similar logic is also used in FORG. Be consistent whenever CALL/CALLC is updated.

                    //CALL/CALLC uses 2 uints.
                    int f = (int)((ii >> 16) & 0xFF);
                    int l1 = (int)((ii >> 8) & 0xFF);
                    int l2 = (int)(ii & 0xFF);
                    ii = inst[pc++];
                    int r1 = (int)((ii >> 16) & 0xFF);

                    stack[0].PC = pc;

                    //Adjust current sig block at the left side.
                    //This allows to merge other arguments that have already been pushed before.
                    if (l2 == 0)
                    {
                        sig.AdjustLeft(in values, proto.SigTypes, l1);
                    }
                    else
                    {
                        sig.TypeId = l1;
                        sig.FLength = proto.SigTypes[l1].FLength; //TODO can we eliminate this?
                        sig.Offset = l2;
                    }
                    var sigStart = sig.Offset;

                    var newFuncP = values[f].Object;
                    if (newFuncP is LClosure lc)
                    {
                        //Save state.
                        stack[0].SigOffset = sigStart;
                        thread.ClosureStack.Push(closure);

                        //Setup proto, closure, and stack.
                        var oldSigType = proto.SigTypes[sig.TypeId];
                        closure = lc;
                        proto = lc.Proto;
                        stack = thread.AllocateNextFrame(ref stack[0], sig.Offset, sig.TotalLength,
                            proto.StackSize, proto.SigTypes[r1].Vararg.HasValue);

                        values = thread.GetFrameValues(ref stack[0]);

                        //Adjust argument list according to the requirement of the callee.
                        //Also remove vararg into separate stack.
                        if (!sig.AdjustRight(in values, -1, oldSigType, proto.SigTypes, proto.ParameterSig))
                        {
                            throw new NotImplementedException();
                        }
                        sig.MoveVararg(in values, thread.VarargStack, ref stack[0].VarargInfo);
                        sig.Clear();
                        goto enterNewLuaFrame;
                    }
                    nonLuaFunc = newFuncP;
                    goto callNonLuaFunction;
                }
                case OpCodes.VARG:
                case OpCodes.VARGC:
                {
                    int r1 = (int)((ii >> 16) & 0xFF);
                    int r2 = (int)((ii >> 8) & 0xFF);
                    int r3 = (sbyte)(byte)(ii & 0xFF);

                    //TODO check whether this will write out of range
                    var pos = sig.TypeId == 0 ? proto.SigRegionOffset : (sig.Offset + sig.TotalLength);
                    for (int i = 0; i < stack[0].VarargInfo.VarargLength; ++i)
                    {
                        values[pos + i] = thread.VarargStack[stack[0].VarargInfo.VarargStart + i];
                    }

                    //Overwrite sig as a vararg.
                    Debug.Assert(proto.SigTypes[proto.VarargSig].FLength == 0);
                    sig.TypeId = proto.VarargSig;
                    sig.FLength = 0;
                    sig.Offset = pos;
                    sig.VLength = stack[0].VarargInfo.VarargLength;

                    //Then adjust to requested (this is needed in assignment statement).
                    if (sig.TypeId == r2)
                    {
                        //Fast path.
                        if (sig.VLength >= r3)
                        {
                            sig.VLength -= r3;
                        }
                        else
                        {
                            values.Span.Slice(sig.Offset + sig.TotalLength, r3 - sig.VLength).Fill(TypedValue.Nil);
                            sig.VLength = 0;
                        }
                        sig.TypeId = r1;
                        sig.FLength = proto.SigTypes[r1].FLength; //TODO can we eliminate this?
                    }
                    else
                    {
                        var adjustmentSuccess = sig.AdjustRight(in values,
                            sig.TypeId, proto.SigTypes[sig.TypeId], proto.SigTypes, r1);
                        Debug.Assert(adjustmentSuccess);
                    }

                    if ((OpCodes)(ii >> 24) == OpCodes.VARGC)
                    {
                        sig.Clear();
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

                    //FORG uses 2 uints.
                    int a = (int)((ii >> 16) & 0xFF);
                    pc += 1;

                    stack[0].PC = pc;

                    //FORG always call with Polymorphic_2 arguments.
                    values[a + 3] = values[a + 1]; //s
                    values[a + 4] = values[a + 2]; //var

                    sig.TypeId = (int)WellKnownStackSignature.Polymorphic_2;
                    sig.FLength = 2;
                    sig.Offset = a + 3;
                    sig.VLength = 0;

                    var sigStart = sig.Offset; //a + 3

                    var newFuncP = values[a].Object;
                    if (newFuncP is LClosure lc)
                    {
                        //Save state.
                        stack[0].SigOffset = sigStart;
                        thread.ClosureStack.Push(closure);

                        //Setup proto, closure, and stack.
                        var oldSigType = proto.SigTypes[sig.TypeId];
                        closure = lc;
                        proto = lc.Proto;
                        stack = thread.AllocateNextFrame(ref stack[0], sig.Offset, sig.TotalLength,
                            proto.StackSize, onSameSeg: false);

                        values = thread.GetFrameValues(ref stack[0]);

                        //Adjust argument list according to the requirement of the callee.
                        //Also remove vararg into separate stack.
                        if (!sig.AdjustRight(in values, -1, oldSigType, proto.SigTypes, proto.ParameterSig))
                        {
                            throw new NotImplementedException();
                        }
                        sig.MoveVararg(in values, thread.VarargStack, ref stack[0].VarargInfo);
                        sig.Clear();
                        goto enterNewLuaFrame;
                    }
                    nonLuaFunc = newFuncP;
                    goto callNonLuaFunction;
                }
                case OpCodes.RET0:
                {
                    //No need to pass anything to caller.
                    sig.TypeId = (int)WellKnownStackSignature.Empty;
                    sig.FLength = 0;
                    goto returnFromLuaFunction;
                }
                case OpCodes.RETN:
                {
                    int l1 = (int)((ii >> 16) & 0xFF);
                    int l2 = (int)((ii >> 8) & 0xFF);

                    //First adjust sig block.
                    if (l2 == 0)
                    {
                        sig.AdjustLeft(in values, proto.SigTypes, l1);
                    }
                    else
                    {
                        sig.TypeId = l1;
                        sig.FLength = proto.SigTypes[l1].FLength; //TODO can we eliminate this?
                        sig.Offset = l2;
                    }

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
                default:
                {
                    Debug.Assert(false);
                    break;
                }
                }
            }

        callNonLuaFunction:
            if (nonLuaFunc is NativeFunctionDelegate nativeFunc)
            {
                //Lua-C# boundary, before.

                //Allocate C# frame.
                //This frame is not too different from a Lua frame.
                //Leave PC = 0 (used by RET0/RETN to check whether last frame is Lua frame).
                var nativeFrame = thread.AllocateNextFrame(ref stack[0], sig.Offset, sig.TotalLength,
                    Options.DefaultNativeStackSize, onSameSeg: true);

                //Native function always accepts unspecialized stack.
                if (!proto.SigTypes[sig.TypeId].IsUnspecialized)
                {
                    proto.SigTypes[sig.TypeId].AdjustStackToUnspecialized(in values);
                }

                //Update sig VLength (it will be adjusted later).
                //Note that Type and Offset don't need to change.
                nextNativeStackInfo.Values = nextNativeStackInfo.AsyncStackInfo.GetFrameValues();

                //Call native function.
                sig.VLength = nativeFunc(nextNativeStackInfo, sig.TotalLength);

                sig.TypeId = (int)WellKnownStackSignature.EmptyV;
                sig.FLength = 0;
                thread.DeallocateFrame(ref nativeFrame);
            }
            else if (nonLuaFunc is AsyncNativeFunctionDelegate asyncNativeFunc)
            {
                //Lua-C# boundary, before.

                //Allocate C# frame.
                //This frame is not too different from a Lua frame.
                //Leave PC = 0 (used by RET0/RETN to check whether last frame is Lua frame).
                var nativeFrame = thread.AllocateNextFrame(ref stack[0], sig.Offset, sig.TotalLength,
                    Options.DefaultNativeStackSize, onSameSeg: true);

                //Native function always accepts unspecialized stack.
                if (!proto.SigTypes[sig.TypeId].IsUnspecialized)
                {
                    proto.SigTypes[sig.TypeId].AdjustStackToUnspecialized(in values);
                }

                //Call native function.
                var retTask = asyncNativeFunc(nextNativeStackInfo.AsyncStackInfo, sig.VLength);
                if (!retTask.IsCompleted)
                {
                    //Yield (need to save the task).
                    throw new NotImplementedException();
                }

                sig.VLength = retTask.Result;
                sig.TypeId = (int)WellKnownStackSignature.EmptyV;
                sig.FLength = 0;
                thread.DeallocateFrame(ref nativeFrame);
            }
            else
            {
                //TODO check metatable
                throw new NotImplementedException();
            }

            //After native function call returns.

            //Lua-C# boundary, after.

            //Check Lua stack relocation.
            //TODO this is slow, need to optimize
            if (!Unsafe.AreSame(ref values[0], ref thread.GetFrameValues(ref stack[0])[0]))
            {
                pc += 1;
                stack[0].PC = pc;

                //TODO Need to set up some flags so that when recovered we start from ret sig adjustment.
                throw new RecoverableException();
            }

            //Follow Lua return protocol: use parentSig for adjustment.
            parentSig.Type = StackSignature.EmptyV;

            goto returnFromNativeFunction;

        returnFromLuaFunction:
            //Jump target when exiting a Lua frame (to either another Lua frame or a native frame).

            nextNativeStackInfo.AsyncStackInfo.StackFrame -= 1;

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
                parentSig.Type = proto.SigTypes[sig.TypeId];
                parentSig.VLength = sig.VLength;
                return;
            }

            //Exit from one Lua frame to another Lua frame.
            //Restore parent frame state and continue.

            //Convert sig type (save it temporarily to parentSig).
            //TODO we should probably find a better place, as parentSig is a ref.
            parentSig.Type = proto.SigTypes[sig.TypeId];

            //Restore.
            closure = thread.ClosureStack.Pop();
            proto = closure.Proto;
            values = thread.GetFrameValues(ref stack[0]);
            sig.Offset = stack[0].SigOffset;
            inst = proto.Instructions;

        returnFromNativeFunction:
            //Jump target from calls to native (C#) functions. These calls also need to adjust the
            //sig block, but it does not require the same restore step.

            ii = inst[pc - 1];
            {
                int r1 = (int)((ii >> 16) & 0xFF);
                int r2 = (int)((ii >> 8) & 0xFF);
                int r3 = (sbyte)(byte)(ii & 0xFF);

                //Adjust return values (without moving additional to vararg list).
                if (parentSig.Type.GlobalId == proto.SigTypes[r2].GlobalId)
                {
                    //Fast path.
                    if (sig.VLength >= r3)
                    {
                        sig.VLength -= r3;
                    }
                    else
                    {
                        values.Span.Slice(sig.Offset + sig.TotalLength, r3 - sig.VLength).Fill(TypedValue.Nil);
                        sig.VLength = 0;
                    }
                    sig.TypeId = r1;
                    sig.FLength = proto.SigTypes[r1].FLength; //TODO can we eliminate this?
                }
                else
                {
                    if (!sig.AdjustRight(in values, -1, parentSig.Type, proto.SigTypes, r1))
                    {
                        throw new NotImplementedException();
                    }
                }

                //TODO maybe we should give CALL/CALLC/FORG different OpCodes for INV
                //so we don't need to read 2 instructions.
                switch ((OpCodes)(ii >> 24))
                {
                case OpCodes.CALL_CTN:
                    Debug.Assert((OpCodes)(inst[pc - 2] >> 24) == OpCodes.CALL);
                    break;
                case OpCodes.CALLC_CTN:
                    Debug.Assert((OpCodes)(inst[pc - 2] >> 24) == OpCodes.CALLC);
                    sig.Clear();
                    break;
                case OpCodes.FORG_CTN:
                    Debug.Assert((OpCodes)(inst[pc - 2] >> 24) == OpCodes.FORG);
                    sig.Clear();

                    //For-loop related logic.
                    ii = inst[pc - 2];
                    int a = (int)((ii >> 16) & 0xFF);

                    if (values[a + 3].Type == VMSpecializationType.Nil)
                    {
                        pc += (short)(ushort)(ii & 0xFFFF);
                    }
                    values[a + 2] = values[a + 3]; //var = var_1
                    break;
                default:
                    Debug.Assert(false);
                    break;
                }
            }
            goto continueOldFrame;
        }
    }
}
