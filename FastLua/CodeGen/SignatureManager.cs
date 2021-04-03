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
        private readonly Dictionary<int, List<StackSignature>> _knownSignatures = new();

        public StackSignature Get(List<VMSpecializationType> fixedList, VMSpecializationType? vararg)
        {
            if (!TryFind(fixedList, vararg, out var ret))
            {
                ret = Create(fixedList, vararg);
                //Already added to list by Create.
            }
            return ret;
        }

        private bool TryFind(List<VMSpecializationType> fixedList, VMSpecializationType? vararg,
            out StackSignature result)
        {
            if (!_knownSignatures.TryGetValue(fixedList.Count, out var list))
            {
                list = new();
                _knownSignatures.Add(fixedList.Count, list);
            }
            foreach (var s in list)
            {
                if (!(vararg == s.Vararg)) //Comparing nullable.
                {
                    continue;
                }
                for (int i = 0; i < fixedList.Count; ++i)
                {
                    if (fixedList[i] != s.ElementInfo[i].type)
                    {
                        continue;
                    }
                }
                result = s;
                return true;
            }
            result = null;
            return false;
        }

        private StackSignature Create(List<VMSpecializationType> fixedList, VMSpecializationType? vararg)
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
            list.Add(nv);
            list.Add(v);
            return vararg.HasValue ? v : nv;
        }
    }
}
