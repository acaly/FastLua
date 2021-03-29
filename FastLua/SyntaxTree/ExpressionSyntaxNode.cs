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
        Single,
        Fixed,
        Variable,
    }

    public abstract class ExpressionSyntaxNode : SyntaxNode
    {
        public virtual ExpressionMultiRetState MultiRetState { get; set; }
        public virtual ExpressionReceiverMultiRetState ReceiverMultiRetState { get; set; }
        public virtual SpecificationType SpecificationType { get; set; }

        internal override void Serialize(BinaryWriter bw)
        {
            base.Serialize(bw);
            SerializeV(bw, MultiRetState);
            SerializeV(bw, ReceiverMultiRetState);
            SerializeV(bw, SpecificationType);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            MultiRetState = DeserializeV<ExpressionMultiRetState>(br);
            ReceiverMultiRetState = DeserializeV<ExpressionReceiverMultiRetState>(br);
            SpecificationType = DeserializeV<SpecificationType>(br);
        }
    }
}
