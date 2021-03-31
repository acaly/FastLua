using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    internal partial class LuaInterpreter
    {
        [InlineSwitch(typeof(LuaInterpreter))]
        private void InterpreterLoop_Template(int op)
        {
            Console.WriteLine(123);
            switch (op)
            {
            default: //placeholder
                break;
            }
        }

        private partial void InterpreterLoop(int op);

        [InlineSwitchCase]
        private void A(int op)
        {
            switch (op)
            {
            case 0:
                Console.WriteLine("0");
                break;
            }
        }
    }
}
