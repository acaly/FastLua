using FastLua.VM;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLuaTest
{
    public class EmptyFunctions
    {
        [Test]
        public void EmptyString()
        {
            var args = Array.Empty<TypedValue>();
            var results = Array.Empty<TypedValue>();
            TestHelper.DoString("", args, results);
        }

        [Test]
        public void EmptyString_Ret()
        {
            var args = Array.Empty<TypedValue>();
            var results = new TypedValue[1];
            TestHelper.DoString("", args, results);
            Assert.AreEqual(LuaValueType.Nil, results[0].ValueType);
        }

        [Test]
        public void SingleLineComment()
        {
            var args = Array.Empty<TypedValue>();
            var results = Array.Empty<TypedValue>();
            TestHelper.DoString("--single line comment", args, results);
        }

        [Test]
        public void MultiLineComment()
        {
            var args = Array.Empty<TypedValue>();
            var results = Array.Empty<TypedValue>();
            TestHelper.DoString("--[[multi \r\n line\r\n comment]]", args, results);
        }
    }
}
