using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.CodeGen
{
    //Abstract representation of a fragment of the stack that is being built.
    internal interface IStackFragment
    {
        int Length { get; }
        int Offset { get; }
        void Build(ref int offset);
    }

    internal class GroupStackFragment : IStackFragment
    {
        private readonly List<IStackFragment> _children = new();

        public int Length { get; private set; }
        public int Offset { get; private set; }

        public void Add(IStackFragment fragment)
        {
            _children.Add(fragment);
        }

        public void Build(ref int offset)
        {
            Offset = offset;
            for (int i = 0; i < _children.Count; ++i)
            {
                _children[i].Build(ref offset);
            }
            Length = offset - Offset;
        }
    }

    internal struct AllocatedLocal
    {
        public BlockStackFragment Owner;
        public int InFragmentOffset;
        public int Type; //0: unspecialized, 1: obj or num.

        public int Offset => Owner.GetTypeOffset(Type) + InFragmentOffset;
    }

    internal class BlockStackFragment : IStackFragment
    {
        private int _unspecializedLength;
        private int _objLength, _numLength;

        public int Length => _unspecializedLength + Math.Max(_objLength, _numLength);
        public int Offset { get; private set; }

        public int GetTypeOffset(int type)
        {
            return type switch
            {
                0 => Offset,
                _ => Offset + _unspecializedLength,
            };
        }

        public AllocatedLocal AddUnspecialized(int size)
        {
            var ret = new AllocatedLocal
            {
                Owner = this,
                InFragmentOffset = Length,
                Type = 0,
            };
            _unspecializedLength += size;
            return ret;
        }

        public AllocatedLocal AddObj(int size)
        {
            var ret = new AllocatedLocal
            {
                Owner = this,
                InFragmentOffset = Length,
                Type = 1,
            };
            _objLength += size;
            return ret;
        }

        public AllocatedLocal AddNum(int size)
        {
            var ret = new AllocatedLocal
            {
                Owner = this,
                InFragmentOffset = Length,
                Type = 2,
            };
            _numLength += size;
            return ret;
        }

        public void Build(ref int offset)
        {
            Offset = offset;
            offset += Length;
        }
    }

    internal class OverlappedStackFragment : IStackFragment
    {
        private readonly List<IStackFragment> _children = new();
        public int Length { get; private set; }
        public int Offset { get; private set; }

        public void Add(IStackFragment child)
        {
            _children.Add(child);
        }

        public void Build(ref int offset)
        {
            int childOffset;
            int childOffsetEnd = offset;
            for (int i = 0; i < _children.Count; ++i)
            {
                childOffset = offset;
                _children[i].Build(ref childOffset);
                childOffsetEnd = Math.Max(childOffsetEnd, childOffset);
            }
            offset = childOffsetEnd;
        }
    }
}
