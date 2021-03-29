using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.Parser
{
    public class LuaCompilerException : Exception
    {
        public LuaCompilerException(string msg) : base(msg)
        {
        }
    }
}
