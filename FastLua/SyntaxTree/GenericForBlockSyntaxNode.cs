using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public sealed class GenericForBlockSyntaxNode : LoopBlockSyntaxNode
    {
        public LocalVariableDefinitionSyntaxNode HiddenVariableF { get; set; }
        public LocalVariableDefinitionSyntaxNode HiddenVariableS { get; set; }
        public LocalVariableDefinitionSyntaxNode HiddenVariableV { get; set; }
        public List<LocalVariableDefinitionSyntaxNode> LoopVariables { get; } = new();
        public ExpressionListSyntaxNode ExpressionList { get; set; }

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<GenericForBlockSyntaxNode>(bw);
            base.Serialize(bw);
            SerializeO(bw, HiddenVariableF);
            SerializeO(bw, HiddenVariableS);
            SerializeO(bw, HiddenVariableV);
            SerializeL(bw, LoopVariables);
            SerializeO(bw, ExpressionList);
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            HiddenVariableF = DeserializeO<LocalVariableDefinitionSyntaxNode>(br);
            HiddenVariableS = DeserializeO<LocalVariableDefinitionSyntaxNode>(br);
            HiddenVariableV = DeserializeO<LocalVariableDefinitionSyntaxNode>(br);
            DeserializeL(br, LoopVariables);
            ExpressionList = DeserializeO<ExpressionListSyntaxNode>(br);
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            HiddenVariableF.Traverse(visitor);
            HiddenVariableS.Traverse(visitor);
            HiddenVariableV.Traverse(visitor);
            LoopVariables.Traverse(visitor);
            ExpressionList.Traverse(visitor);
            visitor.Finish(this);
        }
    }
}
