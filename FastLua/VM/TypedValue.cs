using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    internal struct TypedValue
    {
        public const long NNMarkL = 0x7FF7A50000000000;
        public const long NNMaskL = 0x7FFFFF0000000000;
        public const long NNMark = 0x7FF7A500;
        public const long NNMask = 0x7FFFFF00;

        public double Number;
        public object Object;

        public VMSpecializationType Type
        {
            get
            {
                var d = BitConverter.DoubleToInt64Bits(Number);
                if ((d & NNMaskL) == NNMarkL)
                {
                    return (VMSpecializationType)((d >> 32) & 0xFF);
                }
                return VMSpecializationType.Double;
            }
        }

        public int IntVal
        {
            get => (int)(uint)(ulong)(BitConverter.DoubleToInt64Bits(Number) & 0xFFFFFFFF);
        }

        public double DoubleVal
        {
            get => Number;
        }

        public string StringVal
        {
            get => (string)Object;
        }

        public static readonly TypedValue Nil = new()
        {
            Number = BitConverter.Int64BitsToDouble(NNMarkL | (int)VMSpecializationType.Nil << 32),
        };
        public static readonly TypedValue True = new()
        {
            Number = BitConverter.Int64BitsToDouble(NNMarkL | (int)VMSpecializationType.Nil << 32 | 1),
        };
        public static readonly TypedValue False = new()
        {
            Number = BitConverter.Int64BitsToDouble(NNMarkL | (int)VMSpecializationType.Nil << 32 | 0),
        };
        public static TypedValue MakeInt(int val) => new()
        {
            Number = BitConverter.Int64BitsToDouble(NNMarkL | (int)VMSpecializationType.Int << 32 | (uint)val),
        };
        public static TypedValue MakeDouble(double val) => new()
        {
            Number = (BitConverter.DoubleToInt64Bits(val) & NNMaskL) == NNMarkL ?
                throw new ArgumentException("Signal NaN double") :
                val,
        };
        public static TypedValue MakeString(string val) => new()
        {
            Number = BitConverter.Int64BitsToDouble(NNMarkL | (int)VMSpecializationType.String << 32),
            Object = val,
        };
        public static TypedValue MakeLClosure(LClosure val) => new()
        {
            Number = BitConverter.Int64BitsToDouble(NNMarkL | (int)VMSpecializationType.LClosure << 32),
            Object = val,
        };
        public static TypedValue MakeTable(Table val) => new()
        {
            Number = BitConverter.Int64BitsToDouble(NNMarkL | (int)VMSpecializationType.Table << 32),
            Object = val,
        };
    }
}
