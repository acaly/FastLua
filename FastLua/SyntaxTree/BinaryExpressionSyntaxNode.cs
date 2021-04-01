using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public readonly struct BinaryOperator
    {
        public enum Raw : ushort
        {
            Unknown,
            Add, Sub, Mul, Div, Pow, Mod,
            Conc,
            L, LE, G, GE, E, NE,
            And, Or,
        }

        public Raw V { get; }
        public byte PL { get; }
        public byte PR { get; }

        private BinaryOperator(Raw v, byte l, byte? r = null)
        {
            V = v;
            PL = l;
            PR = r ?? l;
        }

        public static readonly BinaryOperator Unknown = default;
        public static readonly BinaryOperator Add = new(Raw.Add, 6);
        public static readonly BinaryOperator Min = new(Raw.Sub, 6);
        public static readonly BinaryOperator Mul = new(Raw.Mul, 7);
        public static readonly BinaryOperator Div = new(Raw.Div, 7);
        public static readonly BinaryOperator Pow = new(Raw.Pow, 10, 9);
        public static readonly BinaryOperator Mod = new(Raw.Mod, 7);
        public static readonly BinaryOperator Conc = new(Raw.Conc, 5, 4);
        public static readonly BinaryOperator L = new(Raw.L, 3);
        public static readonly BinaryOperator LE = new(Raw.LE, 3);
        public static readonly BinaryOperator G = new(Raw.G, 3);
        public static readonly BinaryOperator GE = new(Raw.GE, 3);
        public static readonly BinaryOperator E = new(Raw.E, 3);
        public static readonly BinaryOperator NE = new(Raw.NE, 3);
        public static readonly BinaryOperator And = new(Raw.And, 2);
        public static readonly BinaryOperator Or = new(Raw.Or, 1);
    }

    public sealed class BinaryExpressionSyntaxNode : ExpressionSyntaxNode
    {
        public BinaryOperator Operator { get; set; }
        public ExpressionSyntaxNode Left { get; set; }
        public ExpressionSyntaxNode Right { get; set; }

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<BinaryExpressionSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeV(bw, Operator);
            SerializeO(bw, Left);
            SerializeO(bw, Right);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            Operator = DeserializeV<BinaryOperator>(br);
            Left = DeserializeO<ExpressionSyntaxNode>(br);
            Right = DeserializeO<ExpressionSyntaxNode>(br);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            Left.Traverse(visitor);
            Right.Traverse(visitor);
            visitor.Finish(this);
        }
    }
}
