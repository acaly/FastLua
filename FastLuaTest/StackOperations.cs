using FastLua.VM;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLuaTest
{
    public class StackOperations
    {
        [Test]
        public void ReturnSingle()
        {
            var args = Array.Empty<TypedValue>();
            var results = new TypedValue[1];
            TestHelper.DoString("return 10", null, args, results);
            Assert.AreEqual(LuaValueType.Number, results[0].ValueType);
            Assert.AreEqual(10.0, results[0].NumberVal);
        }

        [Test]
        public void ReturnLocal()
        {
            var args = Array.Empty<TypedValue>();
            var results = new TypedValue[1];
            TestHelper.DoString("local a = 20 return a", null, args, results);
            Assert.AreEqual(LuaValueType.Number, results[0].ValueType);
            Assert.AreEqual(20.0, results[0].NumberVal);
        }

        [Test]
        public void StackCalculation()
        {
            var args = Array.Empty<TypedValue>();
            var results = new TypedValue[1];
            TestHelper.DoString("local a = 21 local b, c = 2, a - 1 return b * c", null, args, results);
            Assert.AreEqual(LuaValueType.Number, results[0].ValueType);
            Assert.AreEqual(40.0, results[0].NumberVal);
        }
    }
}
