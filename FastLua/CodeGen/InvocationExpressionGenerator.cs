using FastLua.SyntaxTree;
using FastLua.VM;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    internal class InvocationExpressionGenerator : ExpressionGenerator
    {
        private readonly bool _isVarargRet;

        //Return sig type (only when returning single value (ReceiverMultiRetState is Fixed).
        private readonly VMSpecializationType _singleRetType;
        private readonly int _singleRetSigIndex;

        private readonly bool _selfMode;
        private readonly AllocatedLocal _funcTempSlot;

        //Normal mode.
        private readonly ExpressionGenerator _func;

        //Self mode.
        private readonly ExpressionGenerator _table;
        private readonly int _keyConstant;
        private readonly AllocatedLocal _keyTempSlot;

        //Args (both modes).
        private readonly List<(ExpressionGenerator g, AllocatedLocal stack)> _fixedArgs = new();
        private readonly ExpressionGenerator _vararg;
        //_mergedSigFragment = fixed part + _varargStack.
        private readonly GroupStackFragment _mergedSigFragment;
        private readonly GroupStackFragment _varargStack;
        private readonly int _mergedArgSigIndex, _varargArgSigIndex;

        public InvocationExpressionGenerator(GeneratorFactory factory, BlockGenerator block,
            InvocationExpressionSyntaxNode expr)
            : base(0)
        {
            _isVarargRet = expr.ReceiverMultiRetState == ExpressionReceiverMultiRetState.Variable;
            _singleRetType = expr.SpecializationType.GetVMSpecializationType();

            //Function.
            _selfMode = expr.HasSelf;
            if (_selfMode)
            {
                var func = expr.Function as IndexVariableSyntaxNode;
                Debug.Assert(func is not null);
                var key = func.Key as LiteralExpressionSyntaxNode;
                Debug.Assert(key is not null && key.SpecializationType.LuaType == SpecializationLuaType.String);
                _table = factory.CreateExpression(block, func.Table);

                //Key is unspecialized.
                //TODO allow specialized UGET.
                var keyValue = TypedValue.MakeString(key.StringValue);
                _keyConstant = factory.Function.Constants.AddUnspecialized(keyValue);
                _keyTempSlot = block.TempAllocator.Allocate(VMSpecializationType.Polymorphic);

                var funcType = func.SpecializationType.GetVMSpecializationType();
                //We will use UGET, which only works for polymorphic result.
                Debug.Assert(funcType == VMSpecializationType.Polymorphic);
                _funcTempSlot = block.TempAllocator.Allocate(funcType);
            }
            else
            {
                _func = factory.CreateExpression(block, expr.Function);
                if (!_func.TryGetFromStack(out _))
                {
                    _funcTempSlot = block.TempAllocator.Allocate(_func.GetSingleType());
                }
            }

            //Args.
            //TODO currently we are ignoring target function's parameter type. If we ever add speciailized
            //parameters, we need to change how sigWriter is written below to make it effective.
            //This requires recording type info into InvocationExpression.

            //The fragment required to evaluate this expression (including fixed part + optional vararg part).
            _mergedSigFragment = new GroupStackFragment();
            factory.Function.SigBlockFragment.Add(_mergedSigFragment);

            var sigFixedFragment = new SequentialStackFragment();
            _mergedSigFragment.Add(sigFixedFragment);

            var sigWriter = new SignatureWriter();
            void AddParameter(ExpressionGenerator g)
            {
                //Temporarily check expr type to ensure at least the caller will work after
                //an adjustment. This should be changed to match the recorded target type.
                //See comment above.
                Debug.Assert(g.GetSingleType() == VMSpecializationType.Polymorphic);

                var stack = sigFixedFragment.AddSpecializedType(g.GetSingleType());
                g.WritSig(sigWriter);
                _fixedArgs.Add((g, stack));
            }

            if (_selfMode)
            {
                AddParameter(_table);
            }
            if (!expr.Args.HasVararg)
            {
                for (int i = 0; i < expr.Args.Expressions.Count - 1; ++i)
                {
                    AddParameter(factory.CreateExpression(block, expr.Args.Expressions[i]));
                }
            }
            else
            {
                for (int i = 0; i < expr.Args.Expressions.Count - 1; ++i)
                {
                    AddParameter(factory.CreateExpression(block, expr.Args.Expressions[i]));
                }
                //The last is similar but should be a stack fragment instead of a single slot.
                {
                    //stack (group): used in the direct evaluation of the last arg.
                    //stackOverlapFragment (overlapped): used by the last arg + last arg's children.
                    //Note that when adding stackOverlapFragment to _mergedSigFragment (a group),
                    //we are not following the sequential rule but instead following the group rule.
                    //The difference between the two rules means that the result of the last expression
                    //must be on an aligned position (num and obj slot has same index). This is guaranteed
                    //because we get here only when the last expr is appending vararg in its WritSig method,
                    //and the StackSignature always starts vararg at aligned position.
                    //See comment in StackSignatureBuilder.
                    //Also this part is copied to MultiReturnStatementGenerator.
                    var stackOverlapFragment = new OverlappedStackFragment();
                    _mergedSigFragment.Add(stackOverlapFragment);

                    var stack = new GroupStackFragment();
                    stackOverlapFragment.Add(stack);

                    //Push a new sig fragment.
                    var lastOverlapFragment = factory.Function.SigBlockFragment;
                    factory.Function.SigBlockFragment = stackOverlapFragment;

                    //Create the vararg expression.
                    var e = expr.Args.Expressions[^1];
                    var g = factory.CreateExpression(block, e);

                    //Pop the sig fragment.
                    Debug.Assert(factory.Function.SigBlockFragment == stackOverlapFragment);
                    factory.Function.SigBlockFragment = lastOverlapFragment;

                    g.WritSig(sigWriter);
                    _vararg = g;
                    _varargStack = stack;

                    //We also need the sig for the last expr alone (to get its value with EmitGet).
                    var varargSigWriter = new SignatureWriter();
                    g.WritSig(varargSigWriter);
                    _varargArgSigIndex = varargSigWriter.GetSignature(factory.Function.SignatureManager).i;
                }
            }
            _mergedArgSigIndex = sigWriter.GetSignature(factory.Function.SignatureManager).i;

            //Return sig.
            //Note that for multi-return, the parent expression will record a sig type for us. However,
            //only invocation and return support expr list. For most expressions that do not support
            //multi-return, we must record a sig (to be used in CALL/CALLC) ourselves.
            if (!_isVarargRet)
            {
                //TODO we may avoid this allocation by using an internal list inside the manager for
                //single fixed signature.
                var writer = new SignatureWriter();
                writer.AppendFixed(_singleRetType);
                _singleRetSigIndex = writer.GetSignature(factory.Function.SignatureManager).i;
            }
        }

        public override bool TryGetSingleType(out VMSpecializationType type)
        {
            if (_isVarargRet)
            {
                type = default;
                return false;
            }
            type = _singleRetType;
            return true;
        }

        public override void WritSig(SignatureWriter writer)
        {
            if (_isVarargRet)
            {
                //Currently there is no type info for function return value represented in syntax tree.
                //This should be polymorphic as default.
                Debug.Assert(_singleRetType == VMSpecializationType.Polymorphic);
                writer.AppendVararg(VMSpecializationType.Polymorphic);
            }
            else
            {
                writer.AppendFixed(_singleRetType);
            }
        }

        public override bool TryGetFromStack(out AllocatedLocal stackOffset)
        {
            stackOffset = default;
            return false;
        }

        public override void EmitGet(InstructionWriter writer, AllocatedLocal dest)
        {
            Debug.Assert(!_isVarargRet);
            EmitGetInternal(writer, dest.Offset, _singleRetSigIndex, keepSig: false);
        }

        public override void EmitGet(InstructionWriter writer, IStackFragment sigBlock, int sigIndex, bool keepSig)
        {
            Debug.Assert(_isVarargRet);
            EmitGetInternal(writer, sigBlock.Offset, sigIndex, keepSig);
        }

        public override void EmitDiscard(InstructionWriter writer)
        {
            //Similar to EmitGet single-ret mode, but write to the first ret slot, which eliminates
            //the unnecessary MOV instruction after the CALLC.
            Debug.Assert(!_isVarargRet);
            EmitGetInternal(writer, _mergedSigFragment.Offset, _singleRetSigIndex, keepSig: false);
        }

        private void EmitGetInternal(InstructionWriter writer, int dest, int sig, bool keepSig)
        {
            AllocatedLocal functionSlot;
            int pushParamStart;
            if (_selfMode)
            {
                //Evaluate table (at its allocated slot).
                var tableSlot = _fixedArgs[0].stack;
                _table.EmitPrep(writer);
                _table.EmitGet(writer, tableSlot);

                //Get the key from constant table.
                if (_keyTempSlot.Offset > 255 || _keyConstant > 255)
                {
                    throw new NotImplementedException();
                }
                writer.WriteUUU(Opcodes.K, _keyTempSlot.Offset, _keyConstant, 0);

                //Evaluate function using TGET.
                if (_funcTempSlot.Offset > 255 || tableSlot.Offset > 255)
                {
                    throw new NotImplementedException();
                }
                writer.WriteUUU(Opcodes.TGET, _funcTempSlot.Offset, tableSlot.Offset, _keyTempSlot.Offset);
                functionSlot = _funcTempSlot;
                pushParamStart = 1;
            }
            else
            {
                //Evalueate function.
                if (!_func.TryGetFromStack(out functionSlot))
                {
                    functionSlot = _funcTempSlot;
                    _func.EmitPrep(writer);
                    _func.EmitGet(writer, functionSlot);
                }
                pushParamStart = 0;
            }
            for (int i = pushParamStart; i < _fixedArgs.Count; ++i)
            {
                var (g, stack) = _fixedArgs[i];
                g.EmitPrep(writer);
                g.EmitGet(writer, stack);
            }
            if (_vararg is not null)
            {
                _vararg.EmitPrep(writer);
                _vararg.EmitGet(writer, _varargStack, _varargArgSigIndex, keepSig: true);
            }

            //Call!
            var opcode = keepSig ? Opcodes.CALL : Opcodes.CALLC;
            if (functionSlot.Offset > 255 || _mergedArgSigIndex > 255 || sig > 255)
            {
                throw new NotImplementedException();
            }
            writer.WriteUUU(opcode, functionSlot.Offset, _mergedArgSigIndex, sig);

            //Move if requested.
            if (dest != _mergedSigFragment.Offset)
            {
                //Should move only in single-ret mode.
                Debug.Assert(!_isVarargRet);
                //MOV only supports polymorphic.
                Debug.Assert(_singleRetType == VMSpecializationType.Polymorphic);
                if (dest > 255 || _mergedSigFragment.Offset > 255)
                {
                    throw new NotImplementedException();
                }
                writer.WriteUUU(Opcodes.MOV, dest, _mergedSigFragment.Offset, 0);
            }
        }
    }
}
