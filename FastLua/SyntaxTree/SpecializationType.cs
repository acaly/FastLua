using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public enum SpecializationLuaType
    {
        Unspecified,
        Polymorphic,

        Nil,
        Bool,
        Int32,
        Double,
        String,
        CLRFunction,
        CLRDelegate,
        Table,
        Thread,
        UserData,
    }

    public struct SpecializationType
    {
        public SpecializationLuaType LuaType;
        public uint TableSpecialization;
        public uint CLRStructSpecialization;
    }
}
