using FastLua.SyntaxTree;
using FastLua.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    internal static class CodeGenHelper
    {
        public static VMSpecializationType GetVMSpecializationType(this SpecializationType spec)
        {
            return spec.LuaType switch
            {
                SpecializationLuaType.Bool => VMSpecializationType.Bool,
                SpecializationLuaType.Int32 => VMSpecializationType.Int,
                SpecializationLuaType.Double => VMSpecializationType.Double,
                SpecializationLuaType.String => VMSpecializationType.String,
                SpecializationLuaType.CLRDelegate => VMSpecializationType.NClosure,
                SpecializationLuaType.Nil => VMSpecializationType.Nil,
                SpecializationLuaType.Table => VMSpecializationType.Table,
                SpecializationLuaType.Thread => VMSpecializationType.Polymorphic, //No support for thread type in VM.
                SpecializationLuaType.UserData => VMSpecializationType.UserData,
                SpecializationLuaType.Unspecified => VMSpecializationType.Polymorphic,
                SpecializationLuaType.Polymorphic => VMSpecializationType.Polymorphic,
                _ => VMSpecializationType.Polymorphic,
            };
        }

        public static AllocatedLocal AddSpecializedType(this IAllocatableStackFragment frag, VMSpecializationType spec)
        {
            switch (spec & VMSpecializationType.StorageBits)
            {
            case VMSpecializationType.StorageValue:
                return frag.AddNumber();
            case VMSpecializationType.StorageRef:
                return frag.AddObject();
            case VMSpecializationType.StorageBoth:
            default:
                return frag.AddUnspecialized();
            }
        }

        //Currently used to mark the places where Polymorphic is returned simply because type conversion
        //instructions are not supported yet.
        public static VMSpecializationType Deoptimize(this VMSpecializationType type)
        {
            return VMSpecializationType.Polymorphic;
        }
    }
}
