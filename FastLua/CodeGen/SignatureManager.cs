using FastLua.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    internal class SignatureManager
    {
        private int _nextIndex = 0;
        private readonly Dictionary<int, List<(StackSignature s, int i)>> _knownSignatures = new();

        public (StackSignature, int) Get(List<VMSpecializationType> fixedList, VMSpecializationType? vararg)
        {
            if (!TryFind(fixedList, vararg, out var ret))
            {
                ret = Create(fixedList, vararg);
                //Already added to list by Create.
            }
            return ret;
        }

        private bool TryFind(List<VMSpecializationType> fixedList, VMSpecializationType? vararg,
            out (StackSignature, int) result)
        {
            if (!_knownSignatures.TryGetValue(fixedList.Count, out var list))
            {
                list = new();
                _knownSignatures.Add(fixedList.Count, list);
            }
            foreach (var s in list)
            {
                if (!(vararg == s.s.Vararg)) //Comparing nullable.
                {
                    continue;
                }
                for (int i = 0; i < fixedList.Count; ++i)
                {
                    if (fixedList[i] != s.s.ElementInfo[i].type)
                    {
                        continue;
                    }
                }
                result = s;
                return true;
            }
            result = default;
            return false;
        }

        private (StackSignature, int) Create(List<VMSpecializationType> fixedList, VMSpecializationType? vararg)
        {
            if (fixedList.Any(t => t != VMSpecializationType.Unknown && t != VMSpecializationType.Polymorphic))
            {
                throw new NotImplementedException();
            }
            if (vararg.HasValue &&
                vararg != VMSpecializationType.Unknown && vararg != VMSpecializationType.Polymorphic)
            {
                throw new NotImplementedException();
            }
            var (nv, v) = StackSignature.CreateUnspecialized(fixedList.Count);
            var list = _knownSignatures[fixedList.Count];
            var nvr = (nv, _nextIndex++); //We are adding both into the list, but actually only one is requested.
            var vr = (v, _nextIndex++);
            list.Add(nvr);
            list.Add(vr);
            return vararg.HasValue ? vr : nvr;
        }

        public SignatureDesc[] ToArray()
        {
            return _knownSignatures.SelectMany(l => l.Value).Select(s => s.s.GetDesc()).ToArray();
        }
    }
}
