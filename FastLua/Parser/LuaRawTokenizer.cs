using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.Parser
{
    public class LuaRawTokenizer : SplitTokenSequence<LuaRawTokenType>
    {
        public LuaRawTokenizer() : base(Split, LuaRawTokenType.EOS)
        {
        }

        public static LuaRawTokenType GetCharType(char c)
        {
            char cc = (char)c;
            if (char.IsWhiteSpace(cc) || char.IsSeparator(cc) || char.IsControl(cc))
            {
                return LuaRawTokenType.Whitespace;
            }
            if (char.IsSymbol(cc) || char.IsPunctuation(cc))
            {
                if (cc == '_')
                {
                    return LuaRawTokenType.Text;
                }
                return LuaRawTokenType.Symbols;
            }
            return LuaRawTokenType.Text;
        }

        private static bool Split(ref LuaRawTokenType t, char c)
        {
            if (t == LuaRawTokenType.Invalid)
            {
                t = GetCharType(c);
                return true;
            }
            var nt = GetCharType(c);
            var ret = nt == t;
            t = nt;
            return ret;
        }
    }
}
