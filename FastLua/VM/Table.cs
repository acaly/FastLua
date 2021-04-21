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
            public int NextIndex; //Index of next node in _backupList. 0 means no more nodes.
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

        public Table()
        {
            _mainList = new Node[5];
            _backupList = new Node[5];
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
            for (int i = 0; i < sig.FLength; ++i)
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
    }
}
