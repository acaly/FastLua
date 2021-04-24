using FastLua.VM;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLuaTest
{
    public class NativeToLua
    {
        [Test]
        public void TestAdd()
        {
            var closure = TestHelper.Compile("return function(a, b) return a + b end", null);

            var thread = new Thread();
            var stack = thread.AllocateRootCSharpStack(3);

            var retCount = LuaInterpreter.Execute(stack, closure, 0, 0);
            Assert.AreEqual(1, retCount);
            stack.Read(0, out var retVal);

            Assert.AreEqual(LuaValueType.LClosure, retVal.ValueType);
            var retClosure = retVal.LClosureVal;

            //First call at offset 1: add(1, 2).

            stack.Write(1, TypedValue.MakeDouble(1));
            stack.Write(2, TypedValue.MakeDouble(2));
            retCount = LuaInterpreter.Execute(stack, retClosure, 1, 2);
            Assert.AreEqual(1, retCount);
            stack.Read(1, out retVal);

            Assert.AreEqual(LuaValueType.Number, retVal.ValueType);
            Assert.AreEqual(3, retVal.NumberVal);

            //Second call at offset 0: add(3, ret).

            stack.Write(0, TypedValue.MakeDouble(3));
            retCount = LuaInterpreter.Execute(stack, retClosure, 0, 2);
            Assert.AreEqual(1, retCount);
            stack.Read(0, out retVal);

            Assert.AreEqual(LuaValueType.Number, retVal.ValueType);
            Assert.AreEqual(6, retVal.NumberVal);
        }
    }
}
