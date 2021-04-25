using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
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

    internal readonly unsafe struct StackSignature : IEquatable<StackSignature>
    {
        internal unsafe struct StackSignatureData
        {
            public StackSignatureData* _vararg;
            public ulong GlobalId;
            public UnsafeArray<(VMSpecializationType type, int slot)> ElementInfo;
            public UnsafeArray<(int o, int v)> SlotInfo;
            public VMSpecializationType? Vararg;
            public bool IsUnspecialized;
        }

        private static ulong _nextGlobalId = (ulong)WellKnownStackSignature.Next;
        private static readonly UnsafeArray<StackSignatureData> _internalData;

        [Obsolete("Use default(StackSignature)")]
        public static readonly StackSignature Null;
        public static readonly StackSignature Empty;
        public static readonly StackSignature EmptyV;
        public static readonly StackSignature Polymorphic_2;

        internal readonly StackSignatureData* _data;

        public StackSignature(StackSignatureData* data)
        {
            _data = data;
        }

        static StackSignature()
        {
            _internalData = new((int)WellKnownStackSignature.Next + 1);
            _internalData[0] = new()
            {
                _vararg = null,
                GlobalId = (ulong)WellKnownStackSignature.Null,
                ElementInfo = UnsafeArray<(VMSpecializationType, int)>.Null,
                SlotInfo = UnsafeArray<(int, int)>.Null,
                Vararg = null,
                IsUnspecialized = true,
            };
            CreateUnspecialized(0,
                (int)WellKnownStackSignature.Empty,
                (int)WellKnownStackSignature.EmptyV,
                _internalData.GetPointer((int)WellKnownStackSignature.Empty),
                _internalData.GetPointer((int)WellKnownStackSignature.EmptyV));
            CreateUnspecialized(2,
                (int)WellKnownStackSignature.Polymorphic_2,
                null,
                _internalData.GetPointer((int)WellKnownStackSignature.Polymorphic_2),
                _internalData.GetPointer((int)WellKnownStackSignature.Next));
#pragma warning disable CS0618 // Type or member is obsolete
            Null = new(_internalData.GetPointer(0));
#pragma warning restore CS0618 // Type or member is obsolete
            Empty = new(_internalData.GetPointer((int)WellKnownStackSignature.Empty));
            EmptyV = new(_internalData.GetPointer((int)WellKnownStackSignature.EmptyV));
            Polymorphic_2 = new(_internalData.GetPointer((int)WellKnownStackSignature.Polymorphic_2));
        }

        public readonly bool IsNull
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _data == null;
        }

        public readonly ulong GlobalId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _data == null ? 0 : _data->GlobalId;
        }

        public readonly VMSpecializationType? Vararg
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _data == null ? null : _data->Vararg;
        }

        public readonly bool IsUnspecialized
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _data == null ? true : _data->IsUnspecialized;
        }

        public readonly int ElementCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _data == null ? 0 : _data->ElementInfo.Length;
        }

        public readonly int FixedSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _data == null ? 0 : _data->SlotInfo.Length;
        }

        public readonly UnsafeArray<(VMSpecializationType type, int slot)> ElementInfo => _data->ElementInfo;
        public readonly UnsafeArray<(int o, int v)> SlotInfo => _data->SlotInfo;

        //Compatible means all slots of the smaller one fix into the bigger
        //one without needing any conversion, so that directly adjusting
        //length is enough.
        //Note that for types with vararg, the vararg part must also be compatible
        //(can be different but without any moving/conversion).
        //For example, NNO is NOT compatible with NN, since the third element O is
        //not at the aligned position, where a normal vararg should starts.
        public readonly bool IsCompatibleWith(StackSignature sig)
        {
            if (IsUnspecialized && sig.IsUnspecialized)
            {
                //Unspecialized sig are always compatible (with or without vararg).
                return true;
            }
            throw new NotImplementedException();
        }

        public readonly bool IsEndCompatibleWith(StackSignature sig)
        {
            //AdjustLeft cannot change whether the sig has vararg part.
            Debug.Assert(_data->Vararg.HasValue == sig._data->Vararg.HasValue);

            if (IsUnspecialized && sig.IsUnspecialized)
            {
                //Unspecialized sig are always compatible (with or without vararg).
                return true;
            }
            throw new NotImplementedException();
        }

        //Adjust to EmptyV.
        //TODO this method needs to return an adjusted length (or modify sig state struct)
        public readonly void AdjustStackToUnspecialized(in StackFrameValues values, ref int vlen)
        {
            if (!IsUnspecialized)
            {
                vlen += ElementCount;
                return;
            }
            throw new NotImplementedException();
        }

        public readonly bool CheckAndAdjustStackToType(in StackFrameValues values, StackSignature newType, ref int vlen)
        {
            throw new NotImplementedException();
        }

        //public readonly ref readonly StackSignatureData WithVararg(VMSpecializationType type)
        //{
        //    if (IsUnspecialized)
        //    {
        //        return ref _vararg[0];
        //    }
        //    throw new NotImplementedException();
        //}

        public static void CreateUnspecialized(int count, StackSignatureData* novararg, StackSignatureData* vararg)
        {
            CreateUnspecialized(count, null, null, novararg, vararg);
        }

        private static void CreateUnspecialized(int count, ulong? nvid, ulong? vid,
            StackSignatureData* novararg, StackSignatureData* vararg)
        {
            //this is wrong
            *vararg = new StackSignatureData()
            {
                GlobalId = vid ?? Interlocked.Increment(ref _nextGlobalId),
                ElementInfo = UnsafeArray<(VMSpecializationType, int)>.Create(Enumerable.Range(0, count)
                    .Select(i => (VMSpecializationType.Polymorphic, i)).ToArray()),
                SlotInfo = UnsafeArray<(int, int)>.Create(Enumerable.Range(0, count)
                    .Select(i => (i, i)).ToArray()),
                Vararg = VMSpecializationType.Polymorphic,
                IsUnspecialized = true,
            };
            *novararg = new StackSignatureData()
            {
                GlobalId = nvid ?? Interlocked.Increment(ref _nextGlobalId),
                ElementInfo = vararg->ElementInfo,
                SlotInfo = vararg->SlotInfo,
                Vararg = null,
                IsUnspecialized = true,
                _vararg = vararg,
            };
        }

        public static bool operator ==(StackSignature left, StackSignature right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StackSignature left, StackSignature right)
        {
            return !(left == right);
        }

        public override bool Equals(object obj)
        {
            return obj is StackSignature signature && Equals(signature);
        }

        public bool Equals(StackSignature other)
        {
            return (IntPtr)_data == (IntPtr)other._data;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((IntPtr)_data);
        }
    }


    //internal class StackSignature
    //{
    //    private readonly HashSet<ulong> _compatibleSignatures = new();
    //    private StackSignature _vararg { get; init; }
    //
    //    public ulong GlobalId { get; private init; }
    //    public ImmutableArray<(VMSpecializationType type, int slot)> ElementInfo { get; private init; }
    //    public ImmutableArray<(int o, int v)> SlotInfo { get; private init; }
    //    public VMSpecializationType? Vararg { get; private init; }
    //    public bool IsUnspecialized { get; private init; }
    //
    //    public int ElementCount => ElementInfo.Length;
    //    public int FixedSize => SlotInfo.Length;
    //
    //    public static readonly StackSignature Null = new()
    //    {
    //        _vararg = null,
    //        GlobalId = (ulong)WellKnownStackSignature.Null,
    //        ElementInfo = ImmutableArray<(VMSpecializationType, int)>.Empty,
    //        SlotInfo = ImmutableArray<(int, int)>.Empty,
    //        Vararg = null,
    //        IsUnspecialized = true,
    //    };
    //
    //    public static readonly StackSignature Empty = CreateUnspecialized(0,
    //        nvid: (ulong)WellKnownStackSignature.Empty,
    //        vid: (ulong)WellKnownStackSignature.EmptyV).novararg;
    //    public static readonly StackSignature EmptyV = Empty._vararg;
    //
    //    public static readonly StackSignature Polymorphic_2 = CreateUnspecialized(2,
    //        nvid: (ulong)WellKnownStackSignature.Polymorphic_2, vid: null).novararg;
    //
    //    private StackSignature()
    //    {
    //    }
    //
    //    public StackSignature WithVararg(VMSpecializationType type)
    //    {
    //        if (IsUnspecialized)
    //        {
    //            return _vararg;
    //        }
    //        throw new NotImplementedException();
    //    }
    //
    //    //Compatible means all slots of the smaller one fix into the bigger
    //    //one without needing any conversion, so that directly adjusting
    //    //length is enough.
    //    //Note that for types with vararg, the vararg part must also be compatible
    //    //(can be different but without any moving/conversion).
    //    //For example, NNO is NOT compatible with NN, since the third element O is
    //    //not at the aligned position, where a normal vararg should starts.
    //    public bool IsCompatibleWith(StackSignature sig)
    //    {
    //        if (IsUnspecialized && sig.IsUnspecialized)
    //        {
    //            //Unspecialized sig are always compatible (with or without vararg).
    //            return true;
    //        }
    //        return sig.GlobalId == 0 || _compatibleSignatures.Contains(sig.GlobalId);
    //    }
    //
    //    public bool IsEndCompatibleWith(StackSignature sig)
    //    {
    //        //AdjustLeft cannot change whether the sig has vararg part.
    //        Debug.Assert(Vararg.HasValue == sig.Vararg.HasValue);
    //
    //        if (IsUnspecialized && sig.IsUnspecialized)
    //        {
    //            //Unspecialized sig are always compatible (with or without vararg).
    //            return true;
    //        }
    //        throw new NotImplementedException();
    //    }
    //
    //    //Adjust to EmptyV.
    //    //TODO this method needs to return an adjusted length (or modify sig state struct)
    //    public void AdjustStackToUnspecialized(in StackFrameValues values, ref int vlen)
    //    {
    //        if (!IsUnspecialized)
    //        {
    //            vlen += ElementCount;
    //            return;
    //        }
    //        throw new NotImplementedException();
    //    }
    //
    //    public bool CheckAndAdjustStackToType(in StackFrameValues values, StackSignature newType, ref int vlen)
    //    {
    //        throw new NotImplementedException();
    //    }
    //
    //    public static (StackSignature novararg, StackSignature vararg) CreateUnspecialized(int count)
    //    {
    //        return CreateUnspecialized(count, null, null);
    //    }
    //
    //    private static (StackSignature novararg, StackSignature vararg) CreateUnspecialized(int count, ulong? nvid, ulong? vid)
    //    {
    //        var a = new StackSignature()
    //        {
    //            GlobalId = vid ?? Interlocked.Increment(ref _nextGlobalId),
    //            ElementInfo = Enumerable.Range(0, count)
    //                .Select(i => (VMSpecializationType.Polymorphic, i)).ToImmutableArray(),
    //            SlotInfo = Enumerable.Range(0, count)
    //                .Select(i => (i, i)).ToImmutableArray(),
    //            Vararg = VMSpecializationType.Polymorphic,
    //            IsUnspecialized = true,
    //        };
    //        var b = new StackSignature()
    //        {
    //            GlobalId = nvid ?? Interlocked.Increment(ref _nextGlobalId),
    //            ElementInfo = a.ElementInfo,
    //            SlotInfo = a.SlotInfo,
    //            Vararg = null,
    //            IsUnspecialized = true,
    //            _vararg = a,
    //        };
    //        return (b, a);
    //    }
    //
    //    public void CheckCompatibility(StackSignature other)
    //    {
    //        if (IsUnspecialized && other.IsUnspecialized)
    //        {
    //            _compatibleSignatures.Add(other.GlobalId);
    //            other._compatibleSignatures.Add(GlobalId);
    //        }
    //        else
    //        {
    //            throw new NotImplementedException();
    //        }
    //    }
    //}
}
