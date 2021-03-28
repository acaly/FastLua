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
            get => SpecificationType.LuaType == SpecificationLuaType.Bool ? _boolValue : throw new InvalidOperationException();
            set
            {
                SpecificationType = new() { LuaType = SpecificationLuaType.Bool };
                _boolValue = value;
            }
        }

        private long _int64Value;
        public long Int64Value
        {
            get => SpecificationType.LuaType == SpecificationLuaType.Int64 ? _int64Value : throw new InvalidOperationException();
            set
            {
                SpecificationType = new() { LuaType = SpecificationLuaType.Int64 };
                _int64Value = value;
            }
        }

        private double _doubleValue;
        public double DoubleValue
        {
            get => SpecificationType.LuaType == SpecificationLuaType.Double ? _doubleValue : throw new InvalidOperationException();
            set
            {
                SpecificationType = new() { LuaType = SpecificationLuaType.Double };
                _doubleValue = value;
            }
        }

        private string _stringValue;
        private long? _sIntValue;
        private double? _sDoubleValue;
        public string StringValue
        {
            get => SpecificationType.LuaType == SpecificationLuaType.String ? _stringValue : throw new InvalidOperationException();
        }

        public void SetStringValue(string str, long? si, double? sd)
        {
            _stringValue = str;
            _sIntValue = si;
            _sDoubleValue = sd;
            SpecificationType = new() { LuaType = SpecificationLuaType.String };
        }

        public bool CanBeInt64 =>
            SpecificationType.LuaType == SpecificationLuaType.Int64 ||
            _sIntValue.HasValue;

        public bool CanBeDouble =>
            SpecificationType.LuaType == SpecificationLuaType.Int64 ||
            SpecificationType.LuaType == SpecificationLuaType.Double ||
            _sDoubleValue.HasValue;

        public bool CanBeString =>
            SpecificationType.LuaType == SpecificationLuaType.Int64 ||
            SpecificationType.LuaType == SpecificationLuaType.Double ||
            SpecificationType.LuaType == SpecificationLuaType.String;

        public bool AsCondition =>
            !(SpecificationType.LuaType == SpecificationLuaType.Nil ||
                SpecificationType.LuaType == SpecificationLuaType.Bool && !_boolValue);

        internal override void Serialize(BinaryWriter bw)
        {
            SerializeHeader<LiteralExpressionSyntaxNode>(bw);
            base.Serialize(bw);
            switch (SpecificationType.LuaType)
            {
            case SpecificationLuaType.Nil:
                break;
            case SpecificationLuaType.Bool:
                SerializeV(bw, _boolValue);
                break;
            case SpecificationLuaType.Int64:
                SerializeV(bw, _int64Value);
                break;
            case SpecificationLuaType.Double:
                SerializeV(bw, _doubleValue);
                break;
            case SpecificationLuaType.String:
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
            switch (SpecificationType.LuaType)
            {
            case SpecificationLuaType.Nil:
                break;
            case SpecificationLuaType.Bool:
                _boolValue = br.ReadBoolean();
                break;
            case SpecificationLuaType.Int64:
                _int64Value = br.ReadInt64();
                break;
            case SpecificationLuaType.Double:
                _doubleValue = br.ReadDouble();
                break;
            case SpecificationLuaType.String:
                _stringValue = br.ReadString();
                _sIntValue = br.ReadBoolean() ? br.ReadInt64() : null;
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
