using FastLua.VM;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLuaTest
{
    public class FunctionCall
    {
        [Test]
        public void NoRetDiscard()
        {
            var args = Array.Empty<TypedValue>();
            var results = new TypedValue[1];
            TestHelper.DoString(
                "local a = 1 " +
                "local function test() a = a + 1 end " +
                "test() " +
                "test() " +
                "test() " +
                "return a", null, args, results);
            Assert.AreEqual(LuaValueType.Number, results[0].ValueType);
            Assert.AreEqual(4.0, results[0].NumberVal);
        }

        [Test]
        public void NoRetAssignment()
        {
            var args = Array.Empty<TypedValue>();
            var results = new TypedValue[1];
            TestHelper.DoString(
                "local a = 1 " +
                "local function test() a = a + 1 end " +
                "local r11 = test() " +
                "local r21, r22 = test() " +
                "local r31, r32, r33 = test() " +
                "local r41, r42, r43 = 10, 20, test() " +
                "local r51, r52, r53 = 10, 20, 30, test() " +
                "assert(r11 == nil) " +
                "assert(r21 == nil) " +
                "assert(r22 == nil) " +
                "assert(r31 == nil) " +
                "assert(r32 == nil) " +
                "assert(r33 == nil) " +
                "assert(r41 == 10) " +
                "assert(r42 == 20) " +
                "assert(r43 == nil) " +
                "assert(r51 == 10) " +
                "assert(r52 == 20) " +
                "assert(r53 == 30) " +
                "return a", TestHelper.AssertEnv, args, results);
            Assert.AreEqual(LuaValueType.Number, results[0].ValueType);
            Assert.AreEqual(6.0, results[0].NumberVal);
        }

        [Test]
        public void ReturnAssignment()
        {
            var args = Array.Empty<TypedValue>();
            var results = new TypedValue[1];
            TestHelper.DoString(
                "local a = 1 " +
                "local function test() a = a + 1 return 1, 2, 3 end " +
                "local r11, r12, r13, r14 = test() " +
                "local r21, r22, r23, r24 = 10, test() " +
                "local r31, r32, r33 = 10, 20, 30, test() " +
                "local r41, r42, r43 = 10, 20, 30, 40, test() " +
                "assert(r11 == 1) " +
                "assert(r12 == 2) " +
                "assert(r13 == 3) " +
                "assert(r14 == nil) " +
                "assert(r21 == 10) " +
                "assert(r22 == 1) " +
                "assert(r23 == 2) " +
                "assert(r24 == 3) " +
                "assert(r31 == 10) " +
                "assert(r32 == 20) " +
                "assert(r33 == 30) " +
                "assert(r41 == 10) " +
                "assert(r42 == 20) " +
                "assert(r43 == 30) " +
                "return a", TestHelper.AssertEnv, args, results);
            Assert.AreEqual(LuaValueType.Number, results[0].ValueType);
            Assert.AreEqual(5.0, results[0].NumberVal);
        }

        [Test]
        public void NestedCall()
        {
            var args = Array.Empty<TypedValue>();
            var results = Array.Empty<TypedValue>();
            TestHelper.DoString(
                "local function test1(x, y) assert(x == 1) assert(y == 2) return 1, 2 end " +
                "local function test2(x, y) assert(x == 3) assert(y == 4) return 3, 4 end " +
                "local function test3(x, y) assert(x == 1) assert(y == 3) end " +
                "test1(1, 2) " +
                "test2(3, 4) " +
                "test3(test1(1, 2), test2(3, 4))",
                TestHelper.AssertEnv, args, results);
        }

        [Test]
        public void Vararg()
        {
            var args = Array.Empty<TypedValue>();
            var results = Array.Empty<TypedValue>();
            TestHelper.DoString(
                "local function test1(...) " +
                "    local a, b, c, d = ... " +
                "    assert(a == 1) " +
                "    assert(b == 2) " +
                "    assert(c == 3) " +
                "    assert(d == nil) " +
                "end " +
                "local function test2(a, ...) " +
                "    local b, c, d = ... " +
                "    assert(a == 1) " +
                "    assert(b == 2) " +
                "    assert(c == 3) " +
                "    assert(d == nil) " +
                "end " +
                "test1(1, 2, 3) " +
                "test2(1, 2, 3)",
                TestHelper.AssertEnv, args, results);
        }

        //TODO return vararg
        //TODO return vararg as another function's args
    }
}
