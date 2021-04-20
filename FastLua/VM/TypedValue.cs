using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    public enum LuaValueType
    {
        Unknown,
        Nil,
        Bool,
        Number,
        String,
        Table,
        LClosure,
        NClosure,
        UserData,
        Thread,
    }

    public struct TypedValue : IEquatable<TypedValue>
    {
        internal const long NNMarkL = 0x7FF7A50000000000;
        internal const long NNMaskL = 0x7FFFFF0000000000;
        internal const long NNMark = 0x7FF7A500;
        internal const long NNMask = 0x7FFFFF00;

        internal double Number;
        internal object Object;

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

        internal readonly VMSpecializationType Type
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

        public readonly LuaValueType ValueType
        {
            get => Type switch
            {
                VMSpecializationType.Nil => LuaValueType.Nil,
                VMSpecializationType.Bool => LuaValueType.Bool,
                VMSpecializationType.Int => LuaValueType.Number,
                VMSpecializationType.Double => LuaValueType.Number,
                VMSpecializationType.String => LuaValueType.String,
                VMSpecializationType.Table => LuaValueType.Table,
                VMSpecializationType.LClosure => LuaValueType.LClosure,
                VMSpecializationType.NClosure => LuaValueType.NClosure,
                VMSpecializationType.UserData => LuaValueType.UserData,
                VMSpecializationType.Thread => LuaValueType.Thread,
                _ => LuaValueType.Unknown,
            };
        }

        public readonly bool BoolVal
        {
            get => (BitConverter.DoubleToInt64Bits(Number) & 1) != 0;
        }

        public readonly int IntVal
        {
            get => (int)(uint)(ulong)(BitConverter.DoubleToInt64Bits(Number) & 0xFFFFFFFF);
        }

        public readonly double DoubleVal
        {
            get => Number;
        }

        public readonly double NumberVal
        {
            get => Type == VMSpecializationType.Double ? DoubleVal : IntVal;
        }

        public readonly string StringVal
        {
            get => (string)Object;
        }

        public readonly Table TableVal
        {
            get => (Table)Object;
        }

        public readonly LClosure LClosureVal
        {
            get => (LClosure)Object;
        }

        public readonly bool ToBoolVal()
        {
            var d = BitConverter.DoubleToInt64Bits(Number);
            return (d >> 32) switch
            {
                (NNMark | (int)VMSpecializationType.Nil) => false,
                (NNMark | (int)VMSpecializationType.Bool) => (d & 0x1) != 0,
                _ => true,
            };
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
        public static TypedValue MakeNClosure(NativeFunctionDelegate val) => new()
        {
            Number = BitConverter.Int64BitsToDouble(NNMarkL | (long)VMSpecializationType.NClosure << 32),
            Object = val,
        };
        public static TypedValue MakeNClosure(AsyncNativeFunctionDelegate val) => new()
        {
            Number = BitConverter.Int64BitsToDouble(NNMarkL | (long)VMSpecializationType.NClosure << 32),
            Object = val,
        };
        public static TypedValue MakeTable(Table val) => new()
        {
            Number = BitConverter.Int64BitsToDouble(NNMarkL | (long)VMSpecializationType.Table << 32),
            Object = val,
        };
        internal static TypedValue MakeTyped(TypedValue specializedStack, VMSpecializationType type)
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
