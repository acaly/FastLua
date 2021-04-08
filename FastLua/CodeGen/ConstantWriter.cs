using FastLua.VM;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    internal class ConstantWriter
    {
        private readonly List<TypedValue> _values = new();
        private int? _utrue, _ufalse, _unil;

        public int AddUnspecialized(TypedValue value)
        {
            var ret = _values.Count;
            _values.Add(value);
            return ret;
        }

        public int GetUnspecializedTrue()
        {
            if (!_utrue.HasValue)
            {
                _utrue = AddUnspecialized(TypedValue.True);
            }
            return _utrue.Value;
        }

        public int GetUnspecializedFalse()
        {
            if (!_ufalse.HasValue)
            {
                _ufalse = AddUnspecialized(TypedValue.False);
            }
            return _ufalse.Value;
        }

        public int GetUnspecializedNil()
        {
            if (!_unil.HasValue)
            {
                _unil = AddUnspecialized(TypedValue.Nil);
            }
            return _unil.Value;
        }

        public ImmutableArray<TypedValue> ToImmutableArray()
        {
            return _values.ToImmutableArray();
        }
    }
}
