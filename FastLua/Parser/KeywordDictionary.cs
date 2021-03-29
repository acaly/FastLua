﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.Parser
{
    internal class KeywordDictionary : Dictionary<int, (string key, LuaTokenType value)>
    {
        //Although I don't believe CLR implementation will give a collision on Lua keywords, let's be correct.
        private bool _hasCollision = false;
        private Dictionary<string, LuaTokenType> _backup = null;

        public void Add(string kw, LuaTokenType t)
        {
            if (_hasCollision)
            {
                _backup.Add(kw, t);
            }
            else if (!TryAdd(kw.GetHashCode(), (kw, t)))
            {
                MoveToBackup();
                _backup.Add(kw, t);
            }
        }

        private void MoveToBackup()
        {
            _backup = new();
            foreach (var (kw, t) in Values)
            {
                _backup.Add(kw, t);
            }
        }

        public bool TryGetValue(ReadOnlySpan<char> str, out LuaTokenType t)
        {
            if (_hasCollision)
            {
                return _backup.TryGetValue(str.ToString(), out t);
            }
            if (TryGetValue(string.GetHashCode(str), out var info) &&
                str.Equals(info.key, StringComparison.Ordinal))
            {
                t = info.value;
                return true;
            }
            t = default;
            return false;
        }
    }
}
