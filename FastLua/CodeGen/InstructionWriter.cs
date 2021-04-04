using FastLua.SyntaxTree;
using FastLua.VM;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    internal delegate void InstructionFixDelegate(InstructionWriter writer, int instLocation, int labelLocation);

    internal class InstructionWriter
    {
        private readonly List<uint> _instructions = new();
        private readonly Dictionary<LabelStatementSyntaxNode, int> _labels = new();
        private readonly List<(LabelStatementSyntaxNode label, int index, InstructionFixDelegate fix)> _fixes = new();

        private int LastLocation => _instructions.Count - 1;
        private int NextLocation => _instructions.Count;

        public int Count => _instructions.Count;

        public void RunFix()
        {
            for (int i = 0; i <  _fixes.Count; ++i)
            {
                var (label, index, fix) = _fixes[i];
                fix(this, index, _labels[label]);
            }
        }

        private static uint MakeUUU(Opcodes opcode, int a, int b, int c)
        {
            return (uint)opcode << 24 | (uint)a << 16 | (uint)b << 8 | (uint)c;
        }

        private static uint MakeUUS(Opcodes opcode, int a, int b, int c)
        {
            return (uint)opcode << 24 | (uint)a << 16 | (uint)b << 8 | (byte)(sbyte)c;
        }

        private static (Opcodes, int, int, int) DeconstructUUU(uint inst)
        {
            var o = (Opcodes)(inst >> 24);
            var a = (int)((inst >> 16) & 0xFF);
            var b = (int)((inst >> 8) & 0xFF);
            var c = (int)((inst >> 0) & 0xFF);
            return (o, a, b, c);
        }

        private static (Opcodes, int, int, int) DeconstructUUS(uint inst)
        {
            var o = (Opcodes)(inst >> 24);
            var a = (int)((inst >> 16) & 0xFF);
            var b = (int)((inst >> 8) & 0xFF);
            var c = (int)(sbyte)(byte)((inst >> 0) & 0xFF);
            return (o, a, b, c);
        }

        public void WriteUUU(Opcodes opcode, int a, int b, int c)
        {
            _instructions.Add(MakeUUU(opcode, a, b, c));
        }

        public void WriteUUS(Opcodes opcode, int a, int b, int c)
        {
            _instructions.Add(MakeUUS(opcode, a, b, c));
        }

        public (Opcodes, int, int, int) ReadUUU(int index)
        {
            return DeconstructUUU(_instructions[index]);
        }

        public (Opcodes, int, int, int) ReadUUS(int index)
        {
            return DeconstructUUS(_instructions[index]);
        }

        public void ReplaceUUU(int index, Opcodes opcode, int a, int b, int c)
        {
            _instructions[index] = MakeUUU(opcode, a, b, c);
        }

        public void ReplaceUUS(int index, Opcodes opcode, int a, int b, int c)
        {
            _instructions[index] = MakeUUS(opcode, a, b, c);
        }

        //TODO may need to support insert

        //Add a label fix for the last statement written.
        //At the label fix stage, the fix function will be called again with the position of
        //the label to fix the instruction.
        public void AddLabelFix(LabelStatementSyntaxNode label, InstructionFixDelegate fixFunc)
        {
            _fixes.Add((label, LastLocation, fixFunc));
        }

        //Mark the label to point at the next statement.
        public void MarkLabel(LabelStatementSyntaxNode label)
        {
            _labels.Add(label, NextLocation);
        }

        public ImmutableArray<uint> ToImmutableArray()
        {
            return _instructions.ToImmutableArray();
        }
    }
}
