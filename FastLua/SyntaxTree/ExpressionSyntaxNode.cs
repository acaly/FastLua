using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public enum ExpressionMultiRetState
    {
        Never = 0, //No return statement with multiret.
        Not = 1, //Multiret in slow path.
        MayBe = 2, //Not confirmed multiret, initial state.
        MustBe = 3, //Confirmed multiret.
    }

    public enum ExpressionReceiverMultiRetState
    {
        //After parsing, unknown means fixed.
        //We need two, because (...) will set the VarargExpr to a different
        //state than Unknown, so that when it's in expr list, it won't be
        //set to variable again.

        Unknown,
        Fixed,
        Variable,
    }

    public abstract class ExpressionSyntaxNode : SyntaxNode
    {
        public virtual ExpressionMultiRetState MultiRetState { get; set; }
        public virtual ExpressionReceiverMultiRetState ReceiverMultiRetState { get; set; }
        public virtual SpecializationType SpecializationType { get; set; }

        internal override void Serialize(BinaryWriter bw)
        {
            base.Serialize(bw);
            SerializeV(bw, MultiRetState);
            SerializeV(bw, ReceiverMultiRetState);
            SerializeV(bw, SpecializationType);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            MultiRetState = DeserializeV<ExpressionMultiRetState>(br);
            ReceiverMultiRetState = DeserializeV<ExpressionReceiverMultiRetState>(br);
            SpecializationType = DeserializeV<SpecializationType>(br);
        }
    }
}
