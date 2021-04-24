using FastLua.VM;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLuaTest
{
    public class LuaToNative
    {
        [Test]
        public void TestAssert()
        {
            var args = Array.Empty<TypedValue>();
            var results = Array.Empty<TypedValue>();
            TestHelper.DoString("assert(true)", TestHelper.AssertEnv, args, results);
        }

        [Test]
        public void TestAdd()
        {
            var args = Array.Empty<TypedValue>();
            var results = Array.Empty<TypedValue>();
            TestHelper.DoString("assert(add(1, 2) == 3)", TestHelper.DefaultEnv, args, results);
        }
    }
}
