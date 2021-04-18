using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    internal enum VMSpecializationType
    {
        Unknown = 0,

        Nil = 0x11, //Nil should not be used as variable specialization (only for value).
        Bool = 0x12,
        Int = 0x13,
        Double = 0x14,

        String = 0x21,
        Table = 0x22,
        LClosure = 0x23,
        NClosure = 0x24,
        //NFunction = ??,
        UserData = 0x26,

        //No more type for uplist. The region is not typed.
        //UpList = 0x31, //Used internally by VM to store upval lists (TypedValue[]).

        //Above can be used as type of value (as compared to variable).

        MultiTyped = 0x100, //flag
        ValueOnly = 0x110,
        RefOnly = 0x120,
        Polymorphic = 0x130,

        //Masks to check storage usage.

        StorageValue = 0x10,
        StorageRef = 0x20,
        StorageBoth = 0x30,
        StorageBits = 0xF0,
    }

    internal static class VMSpecializationTypeExtensions
    {
        public static (bool num, bool obj) GetStorageType(this VMSpecializationType specType)
        {
            return (specType & VMSpecializationType.StorageBits) switch
            {
                VMSpecializationType.StorageRef => (false, true),
                VMSpecializationType.StorageValue => (true, false),
                _ => (true, true),
            };
        }
    }
}
