using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.VM
{
    public class Table
    {
        private struct Node
        {
            public TypedValue Key;
            public TypedValue Value;

            //Index of next node in _backupList. 0 means no more nodes.
            public int NextIndex;

            //Whether this node has once contained a key. In the case of
            //removed element, the pair is cleared (value set to nil, and
            //key set weakref if it's ref type), and this field remains true.
            //So this by itself should not be used to check whether the
            //node contains valid pair.
            public bool HasKey;
        }

        //Unlike Lua, we use another list to store nodes with collision.
        //Note that index 0 in _backupList is never used.
        //Also, because we support int32 in TypedValue, we have to convert it to double before
        //adding it to _mainList or _backupList.
        private Node[] _mainList, _backupList;
        private TypedValue[] _sequence;
        private int _sequenceSize;
        private int _keyCount;

        //The first free slot in _backupList.
        private int _backupListFreeStart;

        public int SequenceSize => _sequenceSize;

        private const int InitialBucketCount = 5;

        //Expose this property for testing (make collision keys).
        internal int BucketSize => _mainList.Length;

        public Table()
        {
            _mainList = new Node[InitialBucketCount];
            _backupList = new Node[InitialBucketCount];
            _sequence = Array.Empty<TypedValue>();
            _keyCount = 0;
            _backupListFreeStart = 1;
        }

        private int AddBackup()
        {
            Debug.Assert(_backupListFreeStart < _backupList.Length - 1);
            return _backupListFreeStart++;
        }

        private enum FindResult
        {
            NotFoundMain,
            NotFoundBackup,
            Found,
        }

        //Important: the two parameters are not exchangable.
        private static bool CompareKey(ref TypedValue storedKey, ref TypedValue key)
        {
            var stype = storedKey.Type;

            if (stype != key.Type)
            {
                return false;
            }
            if (stype.GetStorageType().obj)
            {
                Debug.Assert(stype.GetStorageType().num == false);
                var sobj = storedKey.Object;
                if (sobj is WeakReference wr)
                {
                    sobj = wr.Target;
                }
                //TODO it's possible that the key provided is a different instance of the
                //same string, and the old key has been garbage collected. Need to check what
                //it does in this case with the original Lua implementation.
                //If the caller is the next function in a for loop, this should never happen.
                if (sobj is string sstr)
                {
                    return sstr == (key.Object as string);
                }
                return ReferenceEquals(sobj, key.Object);
            }
            else
            {
                Debug.Assert(stype.GetStorageType().num == true);
                return BitConverter.DoubleToInt64Bits(storedKey.Number) == BitConverter.DoubleToInt64Bits(key.Number);
            }
        }

        //If found an existing, return its ref. Otherwise, return the last node
        //of the same hash (so a new node can be linked).
        private ref Node FindHashPart(TypedValue key, out FindResult found)
        {
            var index = (uint)key.GetHashCode() % (uint)_mainList.Length;
            if (!_mainList[index].HasKey)
            {
                found = FindResult.NotFoundMain;
                return ref _mainList[index];
            }
            if (CompareKey(ref _mainList[index].Key, ref key))
            {
                found = FindResult.Found;
                return ref _mainList[index];
            }
            var backupIndex = _mainList[index].NextIndex;
            if (backupIndex == 0)
            {
                found = FindResult.NotFoundBackup;
                return ref _mainList[index];
            }
            while (true)
            {
                if (CompareKey(ref _backupList[backupIndex].Key, ref key))
                {
                    found = FindResult.Found;
                    return ref _backupList[backupIndex];
                }
                var nextBackupIndex = _backupList[backupIndex].NextIndex;
                if (nextBackupIndex == 0)
                {
                    found = FindResult.NotFoundBackup;
                    return ref _backupList[backupIndex];
                }
                backupIndex = nextBackupIndex;
            }
        }

        private void SetHashPart(TypedValue key, TypedValue value)
        {
            ref var node = ref FindHashPart(key, out var found);
            switch (found)
            {
            case FindResult.NotFoundMain:
                if (value.Type != VMSpecializationType.Nil)
                {
                    _keyCount += 1;
                    node = new()
                    {
                        Key = key,
                        Value = value,
                        NextIndex = 0,
                        HasKey = true,
                    };
                    CheckRehash();
                }
                break;
            case FindResult.NotFoundBackup:
                if (value.Type != VMSpecializationType.Nil)
                {
                    _keyCount += 1;
                    var index = AddBackup();
                    node.NextIndex = index;
                    _backupList[index] = new()
                    {
                        Key = key,
                        Value = value,
                        NextIndex = 0,
                        HasKey = true,
                    };
                    CheckRehash();
                }
                break;
            case FindResult.Found:
                if (value.Type == VMSpecializationType.Nil)
                {
                    ClearKey(ref node.Key);
                    node.Value = default;
                }
                else
                {
                    RestoreKey(ref node.Key);
                    node.Value = value;
                }
                break;
            }
        }

        private bool GetHashPart(TypedValue key, out TypedValue value)
        {
            ref var node = ref FindHashPart(key, out var found);
            if (found == FindResult.Found)
            {
                value = node.Value;
                return true;
            }
            value = TypedValue.Nil;
            return false;
        }

        private void AppendSequence(TypedValue value)
        {
            void AppendSingle(TypedValue value)
            {
                if (++_sequenceSize > _sequence.Length)
                {
                    var newSeq = new TypedValue[_sequence.Length == 0 ? 4 : _sequence.Length * 2];
                    Array.Copy(_sequence, newSeq, _sequence.Length);
                    _sequence = newSeq;
                }
                _sequence[_sequenceSize - 1] = value;
            }

            AppendSingle(value);
            double key = _sequenceSize + 1;
            while (true)
            {
                ref var node = ref FindHashPart(TypedValue.MakeDouble(key), out var found);
                if (found != FindResult.Found)
                {
                    break;
                }
                AppendSingle(node.Value);
                node.Value = default;
                //Don't clear key. Need it to enumerate.
                key += 1;
            }
        }

        private void RemoveSequence()
        {
            do
            {
                _sequence[--_sequenceSize] = default;
            }
            while (_sequence[_sequenceSize - 1].Type == VMSpecializationType.Nil);
        }

        public void SetRaw(TypedValue key, TypedValue value)
        {
            switch (key.Type)
            {
            case VMSpecializationType.Nil:
                throw new Exception("Table key is nil.");
            case VMSpecializationType.Int:
                var intValue = key.IntVal;
                if (intValue > 0 && intValue <= _sequenceSize)
                {
                    _sequence[intValue - 1] = value;
                    if (intValue == _sequenceSize && value.Type == VMSpecializationType.Nil)
                    {
                        RemoveSequence();
                    }
                    return;
                }
                else if (intValue == _sequenceSize + 1)
                {
                    AppendSequence(value);
                    return;
                }
                key = TypedValue.MakeDouble(intValue);
                break;
            case VMSpecializationType.Double:
                var doubleValue = key.Number;
                var floorDoubleValue = Math.Floor(doubleValue);
                if (floorDoubleValue == doubleValue && floorDoubleValue <= _sequenceSize + 1)
                {
                    key = TypedValue.MakeInt((int)doubleValue);
                    goto case VMSpecializationType.Int;
                }
                break;
            }
            SetHashPart(key, value);
        }

        public bool GetRaw(TypedValue key, out TypedValue value)
        {
            switch (key.Type)
            {
            case VMSpecializationType.Nil:
                throw new Exception("Table key is nil.");
            case VMSpecializationType.Int:
                var intValue = key.IntVal;
                if (intValue > 0 && intValue <= _sequenceSize)
                {
                    value = _sequence[intValue - 1];
                    return true;
                }
                key = TypedValue.MakeDouble(intValue);
                break;
            case VMSpecializationType.Double:
                var doubleValue = key.Number;
                var floorDoubleValue = Math.Floor(doubleValue);
                if (floorDoubleValue == doubleValue &&
                    floorDoubleValue > 0 && floorDoubleValue <= _sequenceSize)
                {
                    key = TypedValue.MakeInt((int)doubleValue);
                    goto case VMSpecializationType.Int;
                }
                break;
            }
            return GetHashPart(key, out value);
        }

        private static int GetHashSize(int minSize)
        {
            return minSize;
        }

        private void CheckRehash()
        {
            if (_keyCount <= _mainList.Length / 2)
            {
                return;
            }

            var oldMain = _mainList;
            var oldBackup = _backupList;
            var newSize = GetHashSize(_keyCount * 4);
            _mainList = new Node[newSize];
            _backupList = new Node[newSize];
            _keyCount = 0;
            _backupListFreeStart = 1;

            for (int i = 0; i < oldMain.Length; ++i)
            {
                if (oldMain[i].HasKey &&
                    oldMain[i].Key.Object is not WeakReference &&
                    oldMain[i].Value.Type != VMSpecializationType.Nil)
                {
                    SetHashPart(oldMain[i].Key, oldMain[i].Value);
                }
            }
            for (int i = 0; i < oldBackup.Length; ++i)
            {
                if (oldBackup[i].HasKey &&
                    oldBackup[i].Key.Object is not WeakReference &&
                    oldBackup[i].Value.Type != VMSpecializationType.Nil)
                {
                    SetHashPart(oldBackup[i].Key, oldBackup[i].Value);
                }
            }
        }

        private static void ClearKey(ref TypedValue key)
        {
            if (key.Type.GetStorageType().obj && key.Object is not null)
            {
                key.Object = new WeakReference(key.Object);
            }
        }

        private static void RestoreKey(ref TypedValue key)
        {
            if (key.Object is WeakReference weakRef)
            {
                Debug.Assert(key.Type.GetStorageType().obj);
                key.Object = weakRef.Target;
            }
        }

        public void Set(TypedValue key, TypedValue value)
        {
            //TODO metatable
            SetRaw(key, value);
        }

        public void Get(TypedValue key, out TypedValue value)
        {
            //TODO metatable
            GetRaw(key, out value);
        }

        internal void SetSequence(Span<TypedValue> values, StackSignature sig)
        {
            for (int i = 0; i < sig.ElementCount; ++i)
            {
                var (type, slot) = sig.ElementInfo[i];
                AppendSequence(TypedValue.MakeTyped(values[slot], type));
            }
            int lastSlot = sig.SlotInfo.Length;
            Debug.Assert(lastSlot == values.Length || sig.Vararg.HasValue);
            if (sig.Vararg.HasValue)
            {
                var varargType = sig.Vararg.Value;
                for (int i = lastSlot; i < values.Length; ++i)
                {
                    AppendSequence(TypedValue.MakeTyped(values[i], varargType));
                }
            }
        }

        //nextIndex is C# index (Lua index - 1).
        private bool Next_FromSequence(int nextIndex, out TypedValue key, out TypedValue value)
        {
            for (int i = nextIndex; i < _sequenceSize; ++i)
            {
                if (_sequence[i].Type != VMSpecializationType.Nil)
                {
                    key = TypedValue.MakeInt(i + 1);
                    value = _sequence[i];
                    return true;
                }
            }
            return Next_FromBucket(0, out key, out value);
        }

        private bool Next_FromBucket(int index, out TypedValue key, out TypedValue value)
        {
            for (int i = index; i < _mainList.Length; ++i)
            {
                if (!_mainList[i].HasKey) continue;
                if (_mainList[i].Key.Object is not WeakReference &&
                    _mainList[i].Value.Type != VMSpecializationType.Nil)
                {
                    key = _mainList[i].Key;
                    value = _mainList[i].Value;
                    return true;
                }
                //TODO this will recursively call the 2 methods for each bucket
                //May need to optimize.
                return Next_FromLastNode(index, ref _mainList[i], out key, out value);
            }
            key = value = TypedValue.Nil;
            return false;
        }

        private bool Next_FromLastNode(int index, ref Node lastNode, out TypedValue key, out TypedValue value)
        {
            var backupIndex = lastNode.NextIndex;
            while (true)
            {
                if (backupIndex == 0)
                {
                    return Next_FromBucket(index + 1, out key, out value);
                }
                if (_backupList[backupIndex].HasKey &&
                    _backupList[backupIndex].Key.Object is not WeakReference &&
                    _backupList[backupIndex].Value.Type != VMSpecializationType.Nil)
                {
                    key = _backupList[backupIndex].Key;
                    value = _backupList[backupIndex].Value;
                    return true;
                }
                backupIndex = _backupList[backupIndex].NextIndex;
            }
        }

        private bool Next_FromHashedPartKey(ref TypedValue key, out TypedValue value)
        {
            var index = (int)((uint)key.GetHashCode() % (uint)_mainList.Length);
            ref var node = ref FindHashPart(key, out var found);
            if (found != FindResult.Found)
            {
                //Last key not found.
                throw new Exception();
            }
            return Next_FromLastNode(index, ref node, out key, out value);
        }

        internal bool Next(ref TypedValue key, out TypedValue value)
        {
            switch (key.Type)
            {
            case VMSpecializationType.Nil:
                return Next_FromSequence(0, out key, out value);
            case VMSpecializationType.Int:
                var intValue = key.IntVal;
                if (intValue > 0 && intValue <= _sequenceSize)
                {
                    //Next C# index == current Lua index == intValue.
                    return Next_FromSequence(intValue, out key, out value);
                }
                key = TypedValue.MakeDouble(intValue);
                break;
            case VMSpecializationType.Double:
                var doubleValue = key.Number;
                var floorDoubleValue = Math.Floor(doubleValue);
                if (floorDoubleValue == doubleValue &&
                    floorDoubleValue > 0 && floorDoubleValue <= _sequenceSize)
                {
                    key = TypedValue.MakeInt((int)doubleValue);
                    goto case VMSpecializationType.Int;
                }
                break;
            }
            return Next_FromHashedPartKey(ref key, out value);
        }
    }
}
