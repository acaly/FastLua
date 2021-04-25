using FastLua.VM;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLuaTest
{
    public class BlockStatements
    {
        [Test]
        public void BlockScope()
        {
            var args = Array.Empty<TypedValue>();
            var results = Array.Empty<TypedValue>();
            TestHelper.DoString(
                "local a = 1 " +
                "do " +
                "    local a = 2 " +
                "    assert(a == 2) " +
                "end " +
                "assert(a == 1) " +
                "do " +
                "    local a = 3 " +
                "    assert(a == 3) " +
                "end " +
                "assert(a == 1)",
                TestHelper.AssertEnv, args, results);
        }

        [Test]
        public void If()
        {
            var args = Array.Empty<TypedValue>();
            var results = new TypedValue[1];
            TestHelper.DoString(
                "local function test1(val) " +
                "    if val == 0 then return 100 end " +
                "    return 200 " +
                "end " +
                "local function test2(val) " +
                "    if val == 0 then " +
                "        return 100 " +
                "    else " +
                "        return 200 " +
                "    end " +
                "end " +
                "local function test3(val) " +
                "    if val == 0 then " +
                "        return 100 " +
                "    elseif val == 1 then " +
                "        return 200 " +
                "    end " +
                "    return 300 " +
                "end " +
                "local function test4(val) " +
                "    if val == 0 then " +
                "        return 100 " +
                "    elseif val == 1 then " +
                "        return 200 " +
                "    else " +
                "        return 300 " +
                "    end " +
                "end " +
                "assert(test1(0) == 100) " +
                "assert(test1(1) == 200) " +
                "assert(test2(0) == 100) " +
                "assert(test2(1) == 200) " +
                "assert(test3(0) == 100) " +
                "assert(test3(1) == 200) " +
                "assert(test3(2) == 300) " +
                "assert(test4(0) == 100) " +
                "assert(test4(1) == 200) " +
                "assert(test4(2) == 300) ",
                TestHelper.AssertEnv, args, results);
        }

        [Test]
        public void While()
        {
            var args = Array.Empty<TypedValue>();
            var results = Array.Empty<TypedValue>();
            TestHelper.DoString(
                "local a, b = 0, 0 " +
                "while a < 5 do " +
                "    a = a + 1 " +
                "    b = b + a " +
                "end " +
                "assert(a == 5) " +
                "assert(b == 15) " +
                "while false do " +
                "    a = 0 " +
                "end " +
                "assert(a == 5)",
                TestHelper.AssertEnv, args, results);
        }

        [Test]
        public void Repeat()
        {
            var args = Array.Empty<TypedValue>();
            var results = Array.Empty<TypedValue>();
            TestHelper.DoString(
                "local a, b = 0, 0 " +
                "repeat " +
                "    a = a + 1 " +
                "    b = b + a " +
                "    local c = a " +
                "until c >= 5 " +
                "assert(a == 5) " +
                "assert(b == 15) " +
                "assert(c == nil)",
                TestHelper.AssertEnv, args, results);
        }

        [Test]
        public void ForNoStep()
        {
            var args = Array.Empty<TypedValue>();
            var results = Array.Empty<TypedValue>();
            TestHelper.DoString(
                "local a, b, c, d = 0, 0, 0, 0 " +
                "for i = 1, 5 do " +
                "    a = a + i " +
                "end " +
                "for i = 5, 1 do " +
                "    b = b + i " +
                "end " +
                "for i = 1.5, 1.5 do " +
                "    c = c + i " +
                "end " +
                "for i = 1.5, 2 do " +
                "    d = d + i " +
                "end " +
                "assert(a == 15) " +
                "assert(b == 0) " +
                "assert(c == 1.5) " +
                "assert(d == 1.5)",
                TestHelper.AssertEnv, args, results);
        }

        [Test]
        public void ForPositive()
        {
            var args = Array.Empty<TypedValue>();
            var results = Array.Empty<TypedValue>();
            TestHelper.DoString(
                "local a, b = 0, 0 " +
                "for i = 1, 5, 0.5 do " +
                "    a = a + i " +
                "end " +
                "for i = 5, 1, 0.5 do " +
                "    b = b + i " +
                "end " +
                "assert(a == 27) " +
                "assert(b == 0)",
                TestHelper.AssertEnv, args, results);
        }

        [Test]
        public void ForNegative()
        {
            var args = Array.Empty<TypedValue>();
            var results = Array.Empty<TypedValue>();
            TestHelper.DoString(
                "local a, b = 0, 0 " +
                "for i = 5, 1, -0.5 do " +
                "    a = a + i " +
                "end " +
                "for i = 1, 5, -0.5 do " +
                "    b = b + i " +
                "end " +
                "assert(a == 27) " +
                "assert(b == 0)",
                TestHelper.AssertEnv, args, results);
        }

        [Test]
        public void ForGeneric()
        {
            var args = Array.Empty<TypedValue>();
            var results = new TypedValue[1];
            TestHelper.DoString(
                "local a = { x = 10, y = 20, 30, 40 } " +
                "local b = {} " +
                "for k, v in next, a do " +
                "    b[k] = v " +
                "end " +
                "return b",
                TestHelper.DefaultEnv, args, results);
            Assert.AreEqual(LuaValueType.Table, results[0].ValueType);
            var table = results[0].TableVal;
            Assert.AreEqual(2, table.SequenceSize);

            Assert.True(table.GetRaw(TypedValue.MakeString("x"), out var val));
            Assert.AreEqual(TypedValue.MakeInt(10), val);
            Assert.True(table.GetRaw(TypedValue.MakeString("y"), out val));
            Assert.AreEqual(TypedValue.MakeInt(20), val);
            Assert.True(table.GetRaw(TypedValue.MakeInt(1), out val));
            Assert.AreEqual(TypedValue.MakeInt(30), val);
            Assert.True(table.GetRaw(TypedValue.MakeInt(2), out val));
            Assert.AreEqual(TypedValue.MakeInt(40), val);
        }
    }
}
