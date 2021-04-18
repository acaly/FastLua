using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        private static ulong _nextGlobalId = 1;
        private readonly HashSet<ulong> _compatibleSignatures = new();
        private StackSignature _vararg { get; init; }

        public ulong GlobalId { get; private init; }
        public ImmutableArray<(VMSpecializationType type, int slot)> ElementInfo { get; private init; }
        public ImmutableArray<(int o, int v)> SlotInfo { get; private init; }
        public VMSpecializationType? Vararg { get; private init; }
        public bool IsUnspecialized { get; private init; }

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

        public SignatureDesc GetDesc()
        {
            if (IsUnspecialized)
            {
                return new SignatureDesc
                {
                    SigType = this,
                    SigTypeId = GlobalId,
                    SigFLength = ElementInfo.Length,
                    HasV = Vararg.HasValue,
                };
            }
            throw new NotImplementedException();
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
        public bool IsCompatibleWith(StackSignature sig)
        {
            if (IsUnspecialized && sig.IsUnspecialized)
            {
                //Unspecialized sig are always compatible (with or without vararg).
                return true;
            }
            return sig.GlobalId == 0 || _compatibleSignatures.Contains(sig.GlobalId);
        }

        public void AdjustStackToUnspecialized()
        {
            if (!IsUnspecialized)
            {
                return;
            }
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
                GlobalId = nvid ?? Interlocked.Increment(ref _nextGlobalId),
                ElementInfo = Enumerable.Range(0, count)
                    .Select(i => (VMSpecializationType.Polymorphic, i)).ToImmutableArray(),
                SlotInfo = Enumerable.Range(0, count)
                    .Select(i => (i, i)).ToImmutableArray(),
                Vararg = VMSpecializationType.Polymorphic,
                IsUnspecialized = true,
            };
            var b = new StackSignature()
            {
                GlobalId = vid ?? Interlocked.Increment(ref _nextGlobalId),
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
