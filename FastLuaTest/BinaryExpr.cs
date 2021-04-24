using FastLua.VM;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLuaTest
{
    public class BinaryExpr
    {
        [Test]
        public void LogicAnd()
        {
            var args = Array.Empty<TypedValue>();
            var results = Array.Empty<TypedValue>();
            TestHelper.DoString(
                "assert((true and 0) == 0) " +
                "assert((true and false) == false) " +
                "assert((true and nil) == nil) " +
                "assert((0 and 1) == 1) " +
                "assert((0 and false) == false) " +
                "assert((false and 1) == false) " +
                "assert((nil and 1) == nil)",
                TestHelper.AssertEnv, args, results);
        }

        [Test]
        public void LogicOr()
        {
            var args = Array.Empty<TypedValue>();
            var results = Array.Empty<TypedValue>();
            TestHelper.DoString(
                "assert((false or 0) == 0) " +
                "assert((false or nil) == nil) " +
                "assert((false or false) == false) " +
                "assert((nil or 0) == 0) " +
                "assert((nil or nil) == nil) " +
                "assert((nil or false) == false) " +
                "assert((true or 0) == true) " +
                "assert((true or nil) == true) " +
                "assert((true or false) == true) " +
                "assert((1 or 0) == 1) " +
                "assert((1 or nil) == 1) " +
                "assert((1 or false) == 1)",
                TestHelper.AssertEnv, args, results);
        }
    }
}
