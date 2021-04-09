using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    public enum Opcodes
    {
        NOP,

        //Comparison.

        ISLT,
        ISLE,
        ISNLT,
        ISNLE,
        ISEQ,
        ISNE,

        //Unary test.

        ISTC,
        ISFC,

        //Unary ops.

        MOV,
        NOT,
        NEG,
        LEN,

        //Binary ops.

        ADD,
        SUB,
        MUL,
        DIV,
        MOD,
        POW,
        CAT,

        ADD_D,

        //Constant.

        K,

        K_D,

        //Upvals.

        UGET,
        USET,
        UNEW,
        FNEW,

        //Table.

        TNEW,
        TGET,
        TSET,

        //Call.

        CALL,
        CALLC,
        VARG,
        VARGC,
        VARG1,

        //Control flow.

        JMP,
        FORI,
        FORL,
        FORG,

        //Signature block.

        SIG,

        //Return.
        //Note: there must be a ret as the last instruction. This
        //is checked by FunctionGenerator. Update the code there if
        //new return opcodes are added.

        RET0,
        RETN,
    }
}
