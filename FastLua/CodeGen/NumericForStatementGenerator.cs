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
    internal sealed class NumericForStatementGenerator : StatementGenerator
    {
        private readonly Opcodes _initOp, _loopOp;
        private readonly ExpressionGenerator _e1, _e2, _e3;
        private readonly AllocatedLocal _e1Stack, _e2Stack, _e3Stack;
        private readonly BlockGenerator _forBlock; //Need this to handle loop var as upval.
        private readonly AllocatedLocal _loopVar;

        public NumericForStatementGenerator(GeneratorFactory factory, BlockGenerator block, NumericForBlockSyntaxNode stat)
        {
            var stack = new GroupStackFragment();
            block.Stack.Add(stack);

            _e1 = factory.CreateExpression(block, stat.From);
            _e2 = factory.CreateExpression(block, stat.To);
            var stepExpr = stat.Step ?? new LiteralExpressionSyntaxNode() { DoubleValue = 1 };
            _e3 = factory.CreateExpression(block, stepExpr);
            var e1t = _e1.GetSingleType();
            var e2t = _e2.GetSingleType();
            var e3t = _e3.GetSingleType();
            if (e1t == VMSpecializationType.Polymorphic &&
                e2t == VMSpecializationType.Polymorphic &&
                e3t == VMSpecializationType.Polymorphic)
            {
                _initOp = Opcodes.FORI;
                _loopOp = Opcodes.FORL;
            }
            else
            {
                throw new NotImplementedException();
            }

            var ctrlStack = new BlockStackFragment();
            stack.Add(ctrlStack);
            _e1Stack = ctrlStack.AddSpecializedType(e1t);
            _e2Stack = ctrlStack.AddSpecializedType(e2t);
            _e3Stack = ctrlStack.AddSpecializedType(e3t);

            //Use stack (so it's after control variables).
            //This will also handle the aux block for us.
            _forBlock = new BlockGenerator(factory, stack, stat);

            var loopVar = factory.Function.Locals[stat.Variable];
            var loopVarOnStack = loopVar.TryGetFromStack(out _loopVar);
            Debug.Assert(loopVarOnStack);
        }

        public override void Emit(InstructionWriter writer)
        {
            var restartLabel = new LabelStatementSyntaxNode();
            var exitLabel = new LabelStatementSyntaxNode();

            if (_e1Stack.Offset > 255 || _loopVar.Offset > 255)
            {
                throw new NotImplementedException();
            }

            //Calc expressions.
            _e1.EmitPrep(writer);
            _e1.EmitGet(writer, _e1Stack);
            _e2.EmitPrep(writer);
            _e2.EmitGet(writer, _e2Stack);
            _e3.EmitPrep(writer);
            _e3.EmitGet(writer, _e3Stack);

            //Init instruction (check type, check exit condition).
            writer.WriteUSx(_initOp, _e1Stack.Offset, 0);
            writer.AddLabelFix(exitLabel, InstructionWriter.FixUSxJump);

            //This is where we restart.
            writer.MarkLabel(restartLabel);

            //Start of the actual block (creates upvals).
            _forBlock.EmitUpvalLists(writer);
            //Copy hidden control variable to the visible loop var.
            writer.WriteUUU(Opcodes.MOV, _loopVar.Offset, _e1Stack.Offset, 0);

            //Emit inner aux block.
            _forBlock.EmitStatements(writer);

            //Loop instruction.
            writer.WriteUSx(_loopOp, _e1Stack.Offset, 0);
            writer.AddLabelFix(restartLabel, InstructionWriter.FixUSxJump);

            writer.MarkLabel(exitLabel);
        }
    }
}
