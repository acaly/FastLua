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
    internal class AssignmentStatementGenerator : StatementGenerator
    {
        private struct AssignmentInfo
        {
            public AllocatedLocal TempSlot;
            public VMSpecializationType Type;
            public ExpressionGenerator VariableGenerator;
            public ExpressionGenerator ExpressionGenerator;
        }

        private readonly List<AssignmentInfo> _oneToOneAssignment = new();
        private readonly List<AssignmentInfo> _variableAssignment = new();
        private readonly ExpressionGenerator _variableValue;
        private readonly ExpressionGenerator _nilValue;
        private AllocatedLocal _nilSlot;
        private readonly int _variableSignature; //Sig index (in proto) of the vararg part on lhs.
        private readonly BlockStackFragment _variableFragment;
        private readonly List<ExpressionGenerator> _unusedValues = new();

        public AssignmentStatementGenerator(GeneratorFactory factory, BlockGenerator parent,
            List<ExpressionGenerator> variables, ExpressionListSyntaxNode exprList)
        {
            int oneToOneCount;
            bool lastAsVararg;
            int unusedStart;
            if (exprList.FixedCount >= variables.Count)
            {
                //Fixed part is enough.
                oneToOneCount = variables.Count;
                lastAsVararg = false;
                unusedStart = variables.Count;
            }
            else if (exprList.HasVararg)
            {
                //Fixed part is not enough, but we have a vararg.
                oneToOneCount = exprList.FixedCount;
                lastAsVararg = true;
                unusedStart = exprList.Expressions.Count;
            }
            else
            {
                //Really not enough. More should set as null.
                oneToOneCount = exprList.Expressions.Count;
                lastAsVararg = false;
                unusedStart = variables.Count;
            }

            for (int i = 0; i < oneToOneCount; ++i)
            {
                //One-to-one part.
                var src = exprList.Expressions[i];
                var dest = variables[i];
                var srcGen = factory.CreateExpression(parent, src);
                var srcHasType = srcGen.TryGetSingleType(out var srcType);
                Debug.Assert(srcHasType);
                AllocatedLocal tmpSlot = default;
                if (!srcGen.TryGetFromStack(out _) && !dest.TryGetFromStack(out _))
                {
                    tmpSlot = parent.TempAllocator.Allocate(srcType);
                }
                _oneToOneAssignment.Add(new AssignmentInfo
                {
                    VariableGenerator = dest,
                    ExpressionGenerator = srcGen,
                    TempSlot = tmpSlot,
                    Type = srcType,
                });
            }
            if (lastAsVararg)
            {
                //Vararg to rest.
                _variableValue = factory.CreateExpression(parent, exprList.Expressions[^1]);
                var sigWriter = new SignatureWriter();
                _variableFragment = new();
                factory.Function.SigBlockFragment.Add(_variableFragment);
                for (int i = oneToOneCount; i < variables.Count; ++i)
                {
                    var v = variables[i];
                    var hasType = v.TryGetSingleType(out var type);
                    Debug.Assert(hasType);
                    sigWriter.AppendFixed(type);
                    var slot = _variableFragment.AddSpecializedType(type);
                    _variableAssignment.Add(new AssignmentInfo
                    {
                        ExpressionGenerator = null,
                        VariableGenerator = v,
                        TempSlot = slot,
                        Type = type,
                    });
                }
                _variableSignature = sigWriter.GetSignature(factory.Function.SignatureManager).i;
            }
            else
            {
                //No vararg. Nil to rest.
                var nilExpr = new LiteralExpressionSyntaxNode
                {
                    SpecializationType = new() { LuaType = SpecializationLuaType.Nil },
                };
                bool needNilValue = false;
                for (int i = oneToOneCount; i < variables.Count; ++i)
                {
                    var v = variables[i];
                    _oneToOneAssignment.Add(new AssignmentInfo
                    {
                        VariableGenerator = v,
                        TempSlot = _nilSlot,
                        Type = VMSpecializationType.Nil,
                    });
                    if (!v.TryGetFromStack(out _))
                    {
                        needNilValue = true;
                    }
                }
                if (needNilValue)
                {
                    _nilValue = factory.CreateExpression(parent, nilExpr);
                    _nilSlot = parent.TempAllocator.Allocate(nilExpr);
                }
            }
            for (int i = unusedStart; i < exprList.Expressions.Count; ++i)
            {
                //More values provided than variables. Add as unused.
                var gen = factory.CreateExpression(parent, exprList.Expressions[i]);
                _unusedValues.Add(gen);
            }
        }

        public override void Emit(InstructionWriter writer)
        {
            //Prepare variables.
            foreach (var v in _oneToOneAssignment)
            {
                v.VariableGenerator.EmitPrep(writer);
            }
            foreach (var v in _variableAssignment)
            {
                v.VariableGenerator.EmitPrep(writer);
            }

            //Nil (used in one-to-one list).
            if (_nilValue is not null)
            {
                _nilValue.EmitPrep(writer);
                _nilValue.EmitGet(writer, _nilSlot);
            }

            //One-to-one assignment.
            foreach (var v in _oneToOneAssignment)
            {
                if (v.ExpressionGenerator.TryGetFromStack(out var exprSlot))
                {
                    v.VariableGenerator.EmitSet(writer, exprSlot, v.Type);
                }
                else if (v.VariableGenerator.TryGetFromStack(out var varSlot))
                {
                    v.ExpressionGenerator.EmitPrep(writer);
                    v.ExpressionGenerator.EmitGet(writer, varSlot);
                }
                else
                {
                    v.ExpressionGenerator.EmitPrep(writer);
                    v.ExpressionGenerator.EmitGet(writer, v.TempSlot);
                    v.VariableGenerator.EmitSet(writer, v.TempSlot, v.Type);
                }
            }

            //Vararg part.
            if (_variableValue is not null)
            {
                _variableValue.EmitPrep(writer);
                _variableValue.EmitGet(writer, _variableFragment, _variableSignature);
                foreach (var v in _variableAssignment)
                {
                    v.VariableGenerator.EmitSet(writer, v.TempSlot, v.Type);
                }
            }

            //Unused part.
            foreach (var v in _unusedValues)
            {
                v.EmitDiscard(writer);
            }
        }
    }
}
