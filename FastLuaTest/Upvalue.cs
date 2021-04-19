using FastLua.VM;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLuaTest
{
    public class Upvalue
    {
        [Test]
        public void Read()
        {
            var args = Array.Empty<TypedValue>();
            var results = new TypedValue[1];
            TestHelper.DoString("local a = 1 local function f() return a end return f()", args, results);
            Assert.AreEqual(LuaValueType.Number, results[0].ValueType);
            Assert.AreEqual(1, results[0].NumberVal);
        }

        //TODO
        //function statement setting upvalue is not working
    }
}
