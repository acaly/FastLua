using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public sealed class SyntaxRoot
    {
        public FunctionDefinitionSyntaxNode RootFunction { get; set; }

        public void SerializeSyntaxTree(BinaryWriter bw)
        {
            RootFunction.Serialize(bw);
        }

        public static SyntaxRoot DeserializeSyntaxTree(BinaryReader br)
        {
            br.ReadInt32(); //Skip type id.

            var ret = new SyntaxRoot()
            {
                RootFunction = new FunctionDefinitionSyntaxNode(),
            };
            ret.RootFunction.Deserialize(br);

            var dict = new Dictionary<ulong, SyntaxNode>();
            ret.RootFunction.Traverse(new SimpleSyntaxTreeVisitor(s => dict.Add(s.DeserializeNodeId, s)));
            ret.RootFunction.Traverse(new SimpleSyntaxTreeVisitor(s => s.SetupReference(dict)));

            return ret;
        }
    }
}
