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
        public void IfStatement()
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
        public void WhileStatement()
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

        //TODO repeat
        //TODO for
    }
}
