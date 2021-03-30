using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public sealed class SyntaxRoot : SyntaxNode
    {
        public FunctionDefinitionSyntaxNode RootFunction { get; set; }
        public List<FunctionDefinitionSyntaxNode> Functions { get; } = new();

        //public void SerializeSyntaxTree(BinaryWriter bw)
        //{
        //    RootFunction.Serialize(bw);
        //    bw.Write(Functions.Count);
        //    foreach (var f in Functions)
        //    {
        //        f.Serialize(bw);
        //    }
        //}
        //
        //public static SyntaxRoot DeserializeSyntaxTree(BinaryReader br)
        //{
        //    br.ReadInt32(); //Skip type id.
        //
        //    var ret = new SyntaxRoot()
        //    {
        //        RootFunction = new FunctionDefinitionSyntaxNode(),
        //    };
        //    ret.RootFunction.Deserialize(br);
        //
        //    var dict = new Dictionary<ulong, SyntaxNode>();
        //    ret.RootFunction.Traverse(new SimpleSyntaxTreeVisitor(s => dict.Add(s.DeserializeNodeId, s)));
        //    ret.RootFunction.Traverse(new SimpleSyntaxTreeVisitor(s => s.SetupReference(dict)));
        //
        //    return ret;
        //}

        internal override void Serialize(BinaryWriter bw)
        {
            //No header.
            base.Serialize(bw);
            SerializeO(bw, RootFunction);
            SerializeL(bw, Functions);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            RootFunction = DeserializeO<FunctionDefinitionSyntaxNode>(br);
            DeserializeL(br, Functions);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            RootFunction.Traverse(visitor);
            Functions.Traverse(visitor);
            visitor.Finish(this);
        }

        public void Write(BinaryWriter bw)
        {
            SerializeO(bw, this);
        }

        public static SyntaxRoot Read(BinaryReader br)
        {
            br.ReadInt32(); //Skip type id.
            var ret = DeserializeO<SyntaxRoot>(br);

            var dict = new Dictionary<ulong, SyntaxNode>();
            ret.RootFunction.Traverse(new SimpleSyntaxTreeVisitor(s => dict.Add(s.DeserializeNodeId, s)));
            ret.RootFunction.Traverse(new SimpleSyntaxTreeVisitor(s => s.SetupReference(dict)));

            return ret;
        }
    }
}
