using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public enum SpecificationLuaType
    {
        Unspecified,
        Polymorphic,

        Nil,
        Bool,
        Int64,
        Double,
        String,
        CLRFunction,
        CLRDelegate,
        Table,
        Thread,
        UserData,
    }

    public struct SpecificationType
    {
        public SpecificationLuaType LuaType;
        public uint TableSpecification;
        public uint CLRStructSpecification;
    }
}
