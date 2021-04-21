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
        private readonly Dictionary<int, List<(StackSignature s, int i)>> _knownSignatures = new();
        private int _nextId = 0;

        public SignatureManager()
        {
            //Add well-known types.
            _knownSignatures.Add(0, new()
            {
                (StackSignature.Null, (int)WellKnownStackSignature.Null),
                (StackSignature.Empty, (int)WellKnownStackSignature.Empty),
                (StackSignature.EmptyV, (int)WellKnownStackSignature.EmptyV),
            });
            _knownSignatures.Add(2, new()
            {
                (StackSignature.Polymorphic_2, (int)WellKnownStackSignature.Polymorphic_2),
            });
            _nextId = (int)WellKnownStackSignature.Next;
        }

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
            out (StackSignature s, int i) result)
        {
            if (!_knownSignatures.TryGetValue(fixedList.Count, out var list))
            {
                list = new();
                _knownSignatures.Add(fixedList.Count, list);
            }
            for (int i = 0; i < list.Count; ++i)
            {
                var (sig, id) = list[i];
                if (vararg != sig.Vararg)
                {
                    continue;
                }
                for (int j = 0; j < fixedList.Count; ++j)
                {
                    if (fixedList[j] != sig.ElementInfo[j].type)
                    {
                        continue;
                    }
                }
                if (id == -1)
                {
                    id = _nextId++;
                    list[i] = (sig, id);
                }
                result = (sig, id);
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
            var nvr = (nv, i: -1);
            var vr = (v, i: -1);
            if (vararg.HasValue)
            {
                vr.i = _nextId++;
            }
            else
            {
                nvr.i = _nextId++;
            }
            list.Add(nvr);
            list.Add(vr);
            return vararg.HasValue ? vr : nvr;
        }

        public StackSignature[] ToArray()
        {
            var ret = new StackSignature[_nextId];
            foreach (var list in _knownSignatures.Values)
            {
                foreach (var (sig, index) in list)
                {
                    if (index != -1)
                    {
                        ret[index] = sig;
                    }
                }
            }
            return ret;
        }
    }
}
