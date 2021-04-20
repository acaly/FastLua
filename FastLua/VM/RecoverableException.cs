using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    //An exception thrown internally in interpreter to restart a specific instruction.
    //This can happen on Lua stack relocation and failure of an optimization guard.
    //This exception is never thrown across C# functions. It is caught at C#-Lua boundary.
    internal class RecoverableException : Exception
    {
    }
}
