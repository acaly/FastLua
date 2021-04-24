using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FastLua.VM
{
    internal enum WellKnownStackSignature
    {
        Null = 0,
        Empty = 1,
        EmptyV = 2,
        Polymorphic_2 = 3,

        Next = 4,
    }

    internal class StackSignature
    {
        private static ulong _nextGlobalId = (ulong)WellKnownStackSignature.Next;
        private readonly HashSet<ulong> _compatibleSignatures = new();
        private StackSignature _vararg { get; init; }

        public ulong GlobalId { get; private init; }
        public ImmutableArray<(VMSpecializationType type, int slot)> ElementInfo { get; private init; }
        public ImmutableArray<(int o, int v)> SlotInfo { get; private init; }
        public VMSpecializationType? Vararg { get; private init; }
        public bool IsUnspecialized { get; private init; }

        public int ElementCount => ElementInfo.Length;
        public int FixedSize => SlotInfo.Length;

        public static readonly StackSignature Null = new()
        {
            _vararg = null,
            GlobalId = (ulong)WellKnownStackSignature.Null,
            ElementInfo = ImmutableArray<(VMSpecializationType, int)>.Empty,
            SlotInfo = ImmutableArray<(int, int)>.Empty,
            Vararg = null,
            IsUnspecialized = true,
        };

        public static readonly StackSignature Empty = CreateUnspecialized(0,
            nvid: (ulong)WellKnownStackSignature.Empty,
            vid: (ulong)WellKnownStackSignature.EmptyV).novararg;
        public static readonly StackSignature EmptyV = Empty._vararg;

        public static readonly StackSignature Polymorphic_2 = CreateUnspecialized(2,
            nvid: (ulong)WellKnownStackSignature.Polymorphic_2, vid: null).novararg;

        private StackSignature()
        {
        }

        public StackSignature WithVararg(VMSpecializationType type)
        {
            if (IsUnspecialized)
            {
                return _vararg;
            }
            throw new NotImplementedException();
        }

        //Compatible means all slots of the smaller one fix into the bigger
        //one without needing any conversion, so that directly adjusting
        //length is enough.
        //Note that for types with vararg, the vararg part must also be compatible
        //(can be different but without any moving/conversion).
        //For example, NNO is NOT compatible with NN, since the third element O is
        //not at the aligned position, where a normal vararg should starts.
        public bool IsCompatibleWith(StackSignature sig)
        {
            if (IsUnspecialized && sig.IsUnspecialized)
            {
                //Unspecialized sig are always compatible (with or without vararg).
                return true;
            }
            return sig.GlobalId == 0 || _compatibleSignatures.Contains(sig.GlobalId);
        }

        public bool IsEndCompatibleWith(StackSignature sig)
        {
            //AdjustLeft cannot change whether the sig has vararg part.
            Debug.Assert(Vararg.HasValue == sig.Vararg.HasValue);

            if (IsUnspecialized && sig.IsUnspecialized)
            {
                //Unspecialized sig are always compatible (with or without vararg).
                return true;
            }
            throw new NotImplementedException();
        }

        //Adjust to EmptyV.
        //TODO this method needs to return an adjusted length (or modify sig state struct)
        public void AdjustStackToUnspecialized(in StackFrameValues values, ref int vlen)
        {
            if (!IsUnspecialized)
            {
                vlen += ElementCount;
                return;
            }
            throw new NotImplementedException();
        }

        public bool CheckAndAdjustStackToType(in StackFrameValues values, StackSignature newType, ref int vlen)
        {
            throw new NotImplementedException();
        }

        public static (StackSignature novararg, StackSignature vararg) CreateUnspecialized(int count)
        {
            return CreateUnspecialized(count, null, null);
        }

        private static (StackSignature novararg, StackSignature vararg) CreateUnspecialized(int count, ulong? nvid, ulong? vid)
        {
            var a = new StackSignature()
            {
                GlobalId = vid ?? Interlocked.Increment(ref _nextGlobalId),
                ElementInfo = Enumerable.Range(0, count)
                    .Select(i => (VMSpecializationType.Polymorphic, i)).ToImmutableArray(),
                SlotInfo = Enumerable.Range(0, count)
                    .Select(i => (i, i)).ToImmutableArray(),
                Vararg = VMSpecializationType.Polymorphic,
                IsUnspecialized = true,
            };
            var b = new StackSignature()
            {
                GlobalId = nvid ?? Interlocked.Increment(ref _nextGlobalId),
                ElementInfo = a.ElementInfo,
                SlotInfo = a.SlotInfo,
                Vararg = null,
                IsUnspecialized = true,
                _vararg = a,
            };
            return (b, a);
        }

        public void CheckCompatibility(StackSignature other)
        {
            if (IsUnspecialized && other.IsUnspecialized)
            {
                _compatibleSignatures.Add(other.GlobalId);
                other._compatibleSignatures.Add(GlobalId);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
