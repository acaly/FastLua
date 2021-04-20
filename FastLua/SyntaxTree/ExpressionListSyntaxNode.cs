using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public sealed class ExpressionListSyntaxNode : SyntaxNode
    {
        public List<ExpressionSyntaxNode> Expressions { get; } = new();

        //Note on the type of the last expression:
        //The last expression may also be specialized, however, there are some differences:
        //1. It must write either one fixed, or one vararg in its WriteSig method.
        //2. When providing vararg, it always starts at an aligned stack slot (see comments
        //   in StackSignatureBuilder).
        //Note that for 1, we currently cannot specialize tuples, but this simplifies the sig
        //adjustment in RETN and CALL/CALLC.

        public bool HasVararg
        {
            get
            {
                if (Expressions.Count == 0)
                {
                    return false;
                }
                if (Expressions[^1].ReceiverMultiRetState != ExpressionReceiverMultiRetState.Variable)
                {
                    return false;
                }
                var lastState = Expressions[^1].MultiRetState;
                return lastState == ExpressionMultiRetState.MayBe ||
                    lastState == ExpressionMultiRetState.MustBe;
            }
        }

        public int FixedCount => HasVararg ? Expressions.Count - 1 : Expressions.Count;

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<ExpressionListSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeL(bw, Expressions);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            DeserializeL(br, Expressions);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            Expressions.Traverse(visitor);
            visitor.Finish(this);
        }
    }
}
