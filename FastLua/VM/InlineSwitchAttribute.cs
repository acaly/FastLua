using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class InlineSwitchAttribute : Attribute
    {
        public Type ImplementationType { get; }

        public InlineSwitchAttribute(Type implType)
        {
            ImplementationType = implType;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class InlineSwitchCaseAttribute : Attribute
    {
        public InlineSwitchCaseAttribute()
        {
        }
    }
}
