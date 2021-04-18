using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    internal struct TypedValue : IEquatable<TypedValue>
    {
        public const long NNMarkL = 0x7FF7A50000000000;
        public const long NNMaskL = 0x7FFFFF0000000000;
        public const long NNMark = 0x7FF7A500;
        public const long NNMask = 0x7FFFFF00;

        public double Number;
        public object Object;

        public override bool Equals(object obj)
        {
            return obj is TypedValue value && Equals(value);
        }

        public bool Equals(TypedValue other)
        {
            return BitConverter.DoubleToInt64Bits(Number) == BitConverter.DoubleToInt64Bits(other.Number) &&
                   EqualityComparer<object>.Default.Equals(Object, other.Object);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Number, Object);
        }

        public static bool operator ==(TypedValue left, TypedValue right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TypedValue left, TypedValue right)
        {
            return !(left == right);
        }

        public readonly VMSpecializationType Type
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

        public readonly int IntVal
        {
            get => (int)(uint)(ulong)(BitConverter.DoubleToInt64Bits(Number) & 0xFFFFFFFF);
        }

        public readonly double DoubleVal
        {
            get => Number;
        }

        public readonly string StringVal
        {
            get => (string)Object;
        }

        public static readonly TypedValue Nil = new()
        {
            Number = BitConverter.Int64BitsToDouble(NNMarkL | (long)VMSpecializationType.Nil << 32),
        };
        public static readonly TypedValue True = new()
        {
            Number = BitConverter.Int64BitsToDouble(NNMarkL | (long)VMSpecializationType.Bool << 32 | 1),
        };
        public static readonly TypedValue False = new()
        {
            Number = BitConverter.Int64BitsToDouble(NNMarkL | (long)VMSpecializationType.Bool << 32 | 0),
        };
        public static TypedValue MakeInt(int val) => new()
        {
            Number = BitConverter.Int64BitsToDouble(NNMarkL | (long)VMSpecializationType.Int << 32 | (uint)val),
        };
        public static TypedValue MakeDouble(double val) => new()
        {
            Number = (BitConverter.DoubleToInt64Bits(val) & NNMaskL) == NNMarkL ?
                throw new ArgumentException("Signal NaN double") :
                val,
        };
        public static TypedValue MakeString(string val) => new()
        {
            Number = BitConverter.Int64BitsToDouble(NNMarkL | (long)VMSpecializationType.String << 32),
            Object = val,
        };
        public static TypedValue MakeLClosure(LClosure val) => new()
        {
            Number = BitConverter.Int64BitsToDouble(NNMarkL | (long)VMSpecializationType.LClosure << 32),
            Object = val,
        };
        public static TypedValue MakeTable(Table val) => new()
        {
            Number = BitConverter.Int64BitsToDouble(NNMarkL | (long)VMSpecializationType.Table << 32),
            Object = val,
        };
        public static TypedValue MakeTyped(TypedValue specializedStack, VMSpecializationType type)
        {
            if (type == VMSpecializationType.Double)
            {
                return MakeDouble(specializedStack.Number);
            }
            else if (!type.GetStorageType().obj)
            {
                specializedStack.Object = null;
                Unsafe.As<double, long>(ref specializedStack.Number) |= NNMarkL | (long)type << 32;
                return specializedStack;
            }
            else
            {
                Unsafe.As<double, long>(ref specializedStack.Number) |= NNMarkL | (long)type << 32;
                return specializedStack;
            }
        }
    }
}
