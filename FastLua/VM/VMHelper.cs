using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    internal static class VMHelper
    {
        public static int? CompareValue(TypedValue a, TypedValue b)
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
                    if (double.IsNaN(a.DoubleVal) || double.IsNaN(b.DoubleVal))
                    {
                        //Math.Sign will throw on NaN values.
                        return null;
                    }
                    return Math.Sign(a.DoubleVal - b.DoubleVal);
                case VMSpecializationType.String:
                    return Comparer<string>.Default.Compare(a.StringVal, b.StringVal);
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
            if (a.Type == VMSpecializationType.String)
            {
                sb.Append(a.StringVal);
            }
            else if (a.Type == VMSpecializationType.Int)
            {
                sb.Append(a.IntVal.ToString());
            }
            else if (a.Type == VMSpecializationType.Double)
            {
                sb.Append(a.DoubleVal.ToString());
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        //Convert unspecialized type to double.
        public static void ForceConvDouble(ref TypedValue val)
        {
            switch (val.Type)
            {
            case VMSpecializationType.Double:
                return;
            case VMSpecializationType.Int:
                val = TypedValue.MakeDouble(val.IntVal);
                return;
            case VMSpecializationType.String:
                if (double.TryParse(val.StringVal, out var parsedVal))
                {
                    val = TypedValue.MakeDouble(parsedVal);
                    return;
                }
                break;
            }
            throw new Exception();
        }

        public static void SetTable(ref TypedValue table, ref TypedValue key, ref TypedValue value)
        {
            if (table.Type == VMSpecializationType.Table)
            {
                ((Table)table.Object).Set(key, value);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public static void GetTable(ref TypedValue table, ref TypedValue key, ref TypedValue value)
        {
            if (table.Type == VMSpecializationType.Table)
            {
                ((Table)table.Object).Get(key, out value);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
