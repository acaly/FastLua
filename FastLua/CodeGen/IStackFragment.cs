using FastLua.VM;
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

    internal interface IAllocatableStackFragment : IStackFragment
    {
        AllocatedLocal AddObject();
        AllocatedLocal AddNumber();
        AllocatedLocal AddUnspecialized();
        int GetTypeOffset(int type);
    }

    internal interface IGroupAllocatableStackFragment : IStackFragment
    {
        void Add(IStackFragment fragment);
    }

    internal class GroupStackFragment : IGroupAllocatableStackFragment
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
        public IAllocatableStackFragment Owner;
        public int InFragmentOffset;
        public int Type;

        public int Offset => Owner.GetTypeOffset(Type) + InFragmentOffset;
    }

    //Allocate local variables. Try to compress the size by grouping slots of
    //the same type together (this will reorder slots).
    internal class BlockStackFragment : IAllocatableStackFragment
    {
        private int _unspecializedLength;
        private int _objLength, _numLength;

        public int Length => _unspecializedLength + Math.Max(_objLength, _numLength);
        public int Offset { get; private set; }

        public int GetTypeOffset(int type)
        {
            //type: 0 unspecialized, 1 obj or num.
            return type switch
            {
                0 => Offset,
                _ => Offset + _unspecializedLength,
            };
        }

        public AllocatedLocal AddUnspecialized()
        {
            var ret = new AllocatedLocal
            {
                Owner = this,
                InFragmentOffset = Length,
                Type = 0,
            };
            _unspecializedLength += 1;
            return ret;
        }

        public AllocatedLocal AddObject()
        {
            var ret = new AllocatedLocal
            {
                Owner = this,
                InFragmentOffset = Length,
                Type = 1,
            };
            _objLength += 1;
            return ret;
        }

        public AllocatedLocal AddNumber()
        {
            var ret = new AllocatedLocal
            {
                Owner = this,
                InFragmentOffset = Length,
                Type = 2,
            };
            _numLength += 1;
            return ret;
        }

        public void Build(ref int offset)
        {
            Offset = offset;
            offset += Length;
        }
    }

    //Similar to BlockStackFragment but unlike it, this class will keep the order of
    //allocated slots in a consistent way the VM's StackSignature class does. This
    //must be used instead of BlockStackFragment when arranging slots in sig block.
    internal class SequentialStackFragment : IAllocatableStackFragment
    {
        private StackSignatureBuilder _builder;

        public int Length => _builder.Length;
        public int Offset { get; private set; }

        public void Build(ref int offset)
        {
            Offset = offset;
            offset += Length;
        }

        public int GetTypeOffset(int type)
        {
            //No type.
            return Offset;
        }

        public AllocatedLocal AddNumber()
        {
            return new AllocatedLocal
            {
                Owner = this,
                InFragmentOffset = _builder.AddNumber(),
                Type = 0,
            };
        }

        public AllocatedLocal AddObject()
        {
            return new AllocatedLocal
            {
                Owner = this,
                InFragmentOffset = _builder.AddObject(),
                Type = 0,
            };
        }

        public AllocatedLocal AddUnspecialized()
        {
            return new AllocatedLocal
            {
                Owner = this,
                InFragmentOffset = _builder.AddUnspecialized(),
                Type = 0,
            };
        }
    }

    internal class OverlappedStackFragment : IGroupAllocatableStackFragment
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
