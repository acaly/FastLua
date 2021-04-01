using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.Parser
{
    public static class LuaParserHelper
    {
        private struct RawNumber
        {
            public bool Sign;
            public bool HasFraction;
            public short Base;
            public int Power;
            public ulong Significand;
        }

        public static double? ParseNumber(ReadOnlySpan<char> str)
        {
            var r = ParseRawNumber(str);
            if (!r.HasValue) return null;
            double ret = r.Value.Significand;
            if (r.Value.Sign) ret = -ret;
            return ret * Math.Pow(r.Value.Base, r.Value.Power);
        }

        public static int? ParseInteger(ReadOnlySpan<char> str)
        {
            return null;
        }

        private static RawNumber? ParseRawNumber(ReadOnlySpan<char> str)
        {
            bool sign = false;
            while (str.Length > 0 && LuaRawTokenizer.GetCharType(str[0]) == LuaRawTokenType.Whitespace)
            {
                str = str[1..];
            }
            if (str.Length > 0 && str[0] == '-')
            {
                sign = true;
                str = str[1..];
            }
            if (str.StartsWith("0x") || str.StartsWith("0X"))
            {
                return Parse(str[2..], DigitHex, 16, 2, 4, sign, 'p', 'P');
            }
            else
            {
                return Parse(str, DigitDec, 10, 10, 1, sign, 'e', 'E');
            }
        }

        private static int? DigitHex(char c)
        {
            return c switch
            {
                >= '0' and <= '9' => c - '0',
                >= 'a' and <= 'f' => c - 'a' + 10,
                >= 'A' and <= 'F' => c - 'A' + 10,
                _ => null,
            };
        }

        private static int? DigitDec(char c)
        {
            return c switch
            {
                >= '0' and <= '9' => c - '0',
                _ => null,
            };
        }

        private static RawNumber? Parse(ReadOnlySpan<char> str, Func<char, int?> digit,
            ushort digitBase, ushort powerBase, int baseDiff, bool sign, char p1, char p2)
        {
            //Significand.
            bool hasFrac = false;
            int? conv;
            ulong significand = 0;
            int powerOffset = 0;
            ulong maxSignificand = ulong.MaxValue / digitBase;
            int decimalOffset = 0;
            while (str.Length > 0 && (conv = digit(str[0])).HasValue)
            {
                if (significand > maxSignificand)
                {
                    //Cannot move full digit width. Move a power base.
                    ulong realMaxSiginificand = ulong.MaxValue / powerBase;
                    int newDigidShift = 1;
                    for (int i = 0; i < baseDiff - 1; ++i)
                    {
                        if (significand > realMaxSiginificand)
                        {
                            break;
                        }
                        significand *= powerBase;
                        newDigidShift *= powerBase;
                        powerOffset -= 1;
                    }
                    significand += (uint)(conv.Value / newDigidShift);
                    break;
                }
                else
                {
                    significand = significand * digitBase + (uint)conv.Value;
                }
                powerOffset -= decimalOffset;
                str = str[1..];
                if (str.Length > 0 && str[0] == '.')
                {
                    if (decimalOffset != 0)
                    {
                        return null;
                    }
                    hasFrac = true;
                    decimalOffset = baseDiff;
                    str = str[1..];
                }
            }
            //More digits, ignore.
            while (str.Length > 0 && (conv = digit(str[0])).HasValue)
            {
            }

            //Power.
            int power = 0;
            if (str.Length > 0 && (str[0] == p1 || str[0] == p2))
            {
                int powerSign = 1;
                str = str[1..];
                if (str[0] == '-')
                {
                    str = str[1..];
                    powerSign = -1;
                }
                if (str.Length == 0 || str[0] < '0' || str[0] > '9') return null;
                do
                {
                    power = power * 10 + (str[0] - '0');
                    str = str[1..];
                } while (str.Length > 0 && str[0] >= '0' && str[0] <= '9');
                power *= powerSign;
            }

            //Finalize.
            while (str.Length > 0 && LuaRawTokenizer.GetCharType(str[0]) == LuaRawTokenType.Whitespace)
            {
                str = str[1..];
            }
            if (str.Length > 0)
            {
                return null;
            }
            return new RawNumber
            {
                Base = (short)powerBase,
                Power = power + powerOffset,
                Significand = significand,
                Sign = sign,
                HasFraction = hasFrac,
            };
        }
    }
}
