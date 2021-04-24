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
            TestHelper.DoString(
                "local a = 1 " +
                "local function get() return a end " +
                "return get()", null, args, results);
            Assert.AreEqual(LuaValueType.Number, results[0].ValueType);
            Assert.AreEqual(1.0, results[0].NumberVal);
        }

        [Test]
        public void Write()
        {
            var args = Array.Empty<TypedValue>();
            var results = new TypedValue[1];
            TestHelper.DoString(
                "local a = 1 " +
                "local function set(val) a = val end " +
                "set(2) " +
                "return a", null, args, results);
            Assert.AreEqual(LuaValueType.Number, results[0].ValueType);
            Assert.AreEqual(2.0, results[0].NumberVal);
        }

        [Test]
        public void WriteRead()
        {
            var args = Array.Empty<TypedValue>();
            var results = new TypedValue[1];
            TestHelper.DoString(
                "local a = 1 " +
                "local function set(val) a = val end " +
                "local function get() return a end " +
                "set(2) " +
                "return get()", null, args, results);
            Assert.AreEqual(LuaValueType.Number, results[0].ValueType);
            Assert.AreEqual(2.0, results[0].NumberVal);
        }

        [Test]
        public void Nested1ImportGlobal()
        {
            var args = Array.Empty<TypedValue>();
            var results = Array.Empty<TypedValue>();
            TestHelper.DoString(
                "local function f1() " +
                "  assert(true) " +
                "  return assert " +
                "end " +
                "f1()(true) ",
                TestHelper.AssertEnv, args, results);
        }

        [Test]
        public void Nested2ImportGlobal()
        {
            var args = Array.Empty<TypedValue>();
            var results = Array.Empty<TypedValue>();
            TestHelper.DoString(
                "local function f1() " +
                "  local function f2() assert(true) end " +
                "  f2() " +
                "  return f2 " +
                "end " +
                "f1()() ",
                TestHelper.AssertEnv, args, results);
        }

        [Test]
        public void Nested1ImportLocal()
        {
            var args = Array.Empty<TypedValue>();
            var results = Array.Empty<TypedValue>();
            TestHelper.DoString(
                "local local_assert" +
                "local function f1() " +
                "  local_assert(true) " +
                "  return local_assert " +
                "end " +
                "local_assert = assert " +
                "f1()(true) ",
                TestHelper.AssertEnv, args, results);
        }

        [Test]
        public void Nested2ImportLocal()
        {
            var args = Array.Empty<TypedValue>();
            var results = Array.Empty<TypedValue>();
            TestHelper.DoString(
                "local local_assert" +
                "local function f1() " +
                "  local function f2() local_assert(true) end " +
                "  f2() " +
                "  return f2 " +
                "end " +
                "local_assert = assert " +
                "f1()() ",
                TestHelper.AssertEnv, args, results);
        }
    }
}
