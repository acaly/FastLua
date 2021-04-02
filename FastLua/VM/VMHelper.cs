﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    internal static class VMHelper
    {
        public static int CompareValue(TypedValue a, TypedValue b)
        {
            var ta = a.Type;
            var tb = b.Type;
            if (ta == tb)
            {
                switch (ta)
                {
                case VMSpecializationType.Int:
                    return Math.Sign(a.IntVal - b.IntVal);
                case VMSpecializationType.Double:
                    return Math.Sign(a.DoubleVal - b.DoubleVal);
                case VMSpecializationType.String:
                    return Comparer<string>.Default.Compare(a.StringVal, b.StringVal);
                default:
                    break;
                }
            }
            throw new NotImplementedException();
        }

        public static bool CompareValueNE(TypedValue a, TypedValue b)
        {
            var ta = a.Type;
            var tb = b.Type;
            if (ta == tb)
            {
                switch (ta)
                {
                case VMSpecializationType.Int:
                    return a.IntVal != b.IntVal;
                case VMSpecializationType.Double:
                    return a.DoubleVal != b.DoubleVal;
                case VMSpecializationType.String:
                    return Comparer<string>.Default.Compare(a.StringVal, b.StringVal) != 0;
                default:
                    break;
                }
            }
            throw new NotImplementedException();
        }

        public static TypedValue UnaryNeg(TypedValue a)
        {
            throw new NotImplementedException();
        }

        public static TypedValue UnaryLen(TypedValue a)
        {
            throw new NotImplementedException();
        }

        public static TypedValue Add(TypedValue a, TypedValue b)
        {
            var ta = a.Type;
            var tb = b.Type;
            if (ta == tb)
            {
                switch (ta)
                {
                case VMSpecializationType.Int:
                    return TypedValue.MakeInt(a.IntVal + b.IntVal);
                case VMSpecializationType.Double:
                    return TypedValue.MakeDouble(a.DoubleVal + b.DoubleVal);
                default:
                    break;
                }
            }
            throw new NotImplementedException();
        }

        public static TypedValue Sub(TypedValue a, TypedValue b)
        {
            var ta = a.Type;
            var tb = b.Type;
            if (ta == tb)
            {
                switch (ta)
                {
                case VMSpecializationType.Int:
                    return TypedValue.MakeInt(a.IntVal - b.IntVal);
                case VMSpecializationType.Double:
                    return TypedValue.MakeDouble(a.DoubleVal - b.DoubleVal);
                default:
                    break;
                }
            }
            throw new NotImplementedException();
        }

        public static TypedValue Mul(TypedValue a, TypedValue b)
        {
            var ta = a.Type;
            var tb = b.Type;
            if (ta == tb)
            {
                switch (ta)
                {
                case VMSpecializationType.Int:
                    return TypedValue.MakeInt(a.IntVal * b.IntVal);
                case VMSpecializationType.Double:
                    return TypedValue.MakeDouble(a.DoubleVal * b.DoubleVal);
                default:
                    break;
                }
            }
            throw new NotImplementedException();
        }

        public static TypedValue Div(TypedValue a, TypedValue b)
        {
            var ta = a.Type;
            var tb = b.Type;
            if (ta == tb)
            {
                switch (ta)
                {
                case VMSpecializationType.Double:
                    return TypedValue.MakeDouble(a.DoubleVal / b.DoubleVal);
                default:
                    break;
                }
            }
            throw new NotImplementedException();
        }

        public static TypedValue Mod(TypedValue a, TypedValue b)
        {
            throw new NotImplementedException();
        }

        public static TypedValue Pow(TypedValue a, TypedValue b)
        {
            throw new NotImplementedException();
        }

        public static void WriteString(StringBuilder sb, TypedValue a)
        {
            throw new NotImplementedException();
        }
    }
}