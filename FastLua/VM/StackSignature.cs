using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FastLua.VM
{
    internal class StackSignature
    {
        private static ulong _nextGlobalId = 1;
        private readonly HashSet<ulong> _compatibleSignatures = new();
        private StackSignature _vararg { get; init; }

        public ulong GlobalId { get; private init; }
        public ImmutableArray<VMSpecializationType> Fixed { get; private init; }
        public VMSpecializationType? Vararg { get; private init; }
        public bool IsUnspecialized { get; private init; }

        public static readonly StackSignature Empty = CreateUnspecialized(0).novararg;
        public static readonly StackSignature EmptyV = Empty._vararg;

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
                    SigFOLength = Fixed.Length,
                    SigFVLength = Fixed.Length,
                    HasVO = Vararg.HasValue,
                    HasVV = Vararg.HasValue,
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
        public bool IsCompatibleWith(ulong id)
        {
            return _compatibleSignatures.Contains(id);
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
            var a = new StackSignature()
            {
                GlobalId = Interlocked.Increment(ref _nextGlobalId),
                Fixed = Enumerable.Repeat(VMSpecializationType.Polymorphic, count).ToImmutableArray(),
                Vararg = VMSpecializationType.Polymorphic,
                IsUnspecialized = true,
            };
            var b = new StackSignature()
            {
                GlobalId = Interlocked.Increment(ref _nextGlobalId),
                Fixed = Enumerable.Repeat(VMSpecializationType.Polymorphic, count).ToImmutableArray(),
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
