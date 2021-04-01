using FastLua.Parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public sealed class LiteralExpressionSyntaxNode : ExpressionSyntaxNode
    {
        //Valid: nil, true, false, number (int/double), string

        private bool _boolValue;
        public bool BoolValue
        {
            get => SpecializationType.LuaType == SpecializationLuaType.Bool ? _boolValue : throw new InvalidOperationException();
            set
            {
                SpecializationType = new() { LuaType = SpecializationLuaType.Bool };
                _boolValue = value;
            }
        }

        private int _int32Value;
        public int Int32Value
        {
            get => SpecializationType.LuaType == SpecializationLuaType.Int32 ? _int32Value : throw new InvalidOperationException();
            set
            {
                SpecializationType = new() { LuaType = SpecializationLuaType.Int32 };
                _int32Value = value;
            }
        }

        private double _doubleValue;
        public double DoubleValue
        {
            get => SpecializationType.LuaType == SpecializationLuaType.Double ? _doubleValue : throw new InvalidOperationException();
            set
            {
                SpecializationType = new() { LuaType = SpecializationLuaType.Double };
                _doubleValue = value;
            }
        }

        private string _stringValue;
        private int? _sIntValue;
        private double? _sDoubleValue;
        public string StringValue
        {
            get => SpecializationType.LuaType == SpecializationLuaType.String ? _stringValue : throw new InvalidOperationException();
            set
            {
                SpecializationType = new() { LuaType = SpecializationLuaType.String };
                _stringValue = value;
                _sIntValue = LuaParserHelper.ParseInteger(value);
                _sDoubleValue = LuaParserHelper.ParseNumber(value);
            }
        }

        public void SetStringValue(string str, int? si, double? sd)
        {
            _stringValue = str;
            _sIntValue = si;
            _sDoubleValue = sd;
            SpecializationType = new() { LuaType = SpecializationLuaType.String };
        }

        public bool CanBeInt64 =>
            SpecializationType.LuaType == SpecializationLuaType.Int32 ||
            _sIntValue.HasValue;

        public bool CanBeDouble =>
            SpecializationType.LuaType == SpecializationLuaType.Int32 ||
            SpecializationType.LuaType == SpecializationLuaType.Double ||
            _sDoubleValue.HasValue;

        public bool CanBeString =>
            SpecializationType.LuaType == SpecializationLuaType.Int32 ||
            SpecializationType.LuaType == SpecializationLuaType.Double ||
            SpecializationType.LuaType == SpecializationLuaType.String;

        public bool AsCondition =>
            !(SpecializationType.LuaType == SpecializationLuaType.Nil ||
                SpecializationType.LuaType == SpecializationLuaType.Bool && !_boolValue);

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<LiteralExpressionSyntaxNode>(bw);
            base.Serialize(bw);
            switch (SpecializationType.LuaType)
            {
            case SpecializationLuaType.Nil:
                break;
            case SpecializationLuaType.Bool:
                SerializeV(bw, _boolValue);
                break;
            case SpecializationLuaType.Int32:
                SerializeV(bw, _int32Value);
                break;
            case SpecializationLuaType.Double:
                SerializeV(bw, _doubleValue);
                SerializeV(bw, _int32Value);
                break;
            case SpecializationLuaType.String:
                bw.Write(_stringValue);
                bw.Write(_sIntValue.HasValue);
                if (_sIntValue.HasValue)
                {
                    bw.Write(_sIntValue.Value);
                }
                bw.Write(_sDoubleValue.HasValue);
                if (_sDoubleValue.HasValue)
                {
                    bw.Write(_sDoubleValue.Value);
                }
                break;
            }
        }

        internal override void Deserialize(BinaryReader br)
        {
            base.Deserialize(br);
            switch (SpecializationType.LuaType)
            {
            case SpecializationLuaType.Nil:
                break;
            case SpecializationLuaType.Bool:
                _boolValue = br.ReadBoolean();
                break;
            case SpecializationLuaType.Int32:
                _int32Value = br.ReadInt32();
                break;
            case SpecializationLuaType.Double:
                _doubleValue = br.ReadDouble();
                _int32Value = br.ReadInt32();
                break;
            case SpecializationLuaType.String:
                _stringValue = br.ReadString();
                _sIntValue = br.ReadBoolean() ? br.ReadInt32() : null;
                _sDoubleValue = br.ReadBoolean() ? br.ReadDouble() : null;
                break;
            }
        }

        public override void Traverse(ISyntaxTreeVisitor visitor)
        {
            visitor.Visit(this);
            visitor.Start(this);
            base.Traverse(visitor);
            visitor.Finish(this);
        }
    }
}
