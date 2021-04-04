using FastLua.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    internal class SignatureWriter
    {
        private readonly List<VMSpecializationType> _fixed = new();
        private VMSpecializationType? _vararg;

        public void Clear()
        {
            _fixed.Clear();
            _vararg = null;
        }

        public StackSignature GetSignature(SignatureManager manager)
        {
            return manager.Get(_fixed, _vararg);
        }

        public void AppendFixed(VMSpecializationType type)
        {
            if (_vararg.HasValue)
            {
                throw new InvalidOperationException();
            }
            _fixed.Add(type);
        }

        public void AppendVararg(VMSpecializationType type)
        {
            if (_vararg.HasValue)
            {
                throw new InvalidOperationException();
            }
            _vararg = type;
        }
    }
}
