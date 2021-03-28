using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FastLua.SyntaxTree
{
    public abstract class SyntaxNode
    {
        private class PropertySerializer<T>
        {
            private static readonly bool _isNode = typeof(T) == typeof(SyntaxNode);
            private static readonly int _len = RuntimeHelpers.IsReferenceOrContainsReferences<T>() ?
                Marshal.SizeOf<T>() : 0;

            public static void Serialize(BinaryWriter bw, T val)
            {
                if (_isNode)
                {
                    ((SyntaxNode)(object)val).Serialize(bw);
                }
                else if (_len > 0)
                {
                    bw.Write(MemoryMarshal.CreateSpan(ref Unsafe.As<T, byte>(ref val), _len));
                }
                else
                {
                    //Should not happen.
                    throw new InvalidOperationException();
                }
            }

            public static T Deserialize(BinaryReader br)
            {
                if (_isNode)
                {
                    var obj = ToType<SyntaxNode>(br.ReadInt32());
                    obj.Deserialize(br);
                    return (T)(object)obj;
                }
                else if (_len > 0)
                {
                    T ret = default;
                    br.Read(MemoryMarshal.CreateSpan(ref Unsafe.As<T, byte>(ref ret), _len));
                    return ret;
                }
                else
                {
                    //Should not happen.
                    throw new InvalidOperationException();
                }
            }
        }

        //It's possible to have duplicate entries (same value, different key) here but they
        //won't affect (de)serialization (unless you get ~2^32 threads serializing simultaneously).
        private static readonly ConcurrentDictionary<int, Type> _deserializeDict = new();
        private static readonly ConcurrentDictionary<Type, int> _serializeDict = new();
        private static int _nextId = 1;
        private static ulong _nextObjId = 1;
        public ulong NodeId { get; private set; }
        public ulong DeserializeNodeId { get; private set; }

        protected SyntaxNode()
        {
            NodeId = Interlocked.Increment(ref _nextObjId);
        }

        internal virtual void Serialize(BinaryWriter bw)
        {
            bw.Write(NodeId);
        }

        internal virtual void Deserialize(BinaryReader br)
        {
            DeserializeNodeId = br.ReadUInt64();
        }

        public virtual void Traverse(ISyntaxTreeVisitor visitor)
        {
        }

        internal virtual void SetupReference(Dictionary<ulong, SyntaxNode> dict)
        {
        }

        private static int ToId<T>() where T : new()
        {
            if (!_serializeDict.TryGetValue(typeof(T), out var ret))
            {
                ret = Interlocked.Increment(ref _nextId);
                _deserializeDict.TryAdd(ret, typeof(T)); //This always succeed.
                ret = _serializeDict.GetOrAdd(typeof(T), ret); //We may got an old value.
            }
            return ret;
        }

        private static T ToType<T>(int t)
        {
            if (!_deserializeDict.TryGetValue(t, out var ret))
            {
                throw new KeyNotFoundException();
            }
            return (T)Activator.CreateInstance(ret);
        }

        protected static void SerializeHeader<T>(BinaryWriter bw) where T : new()
        {
            bw.Write(ToId<T>());
        }

        protected static unsafe void SerializeV<T>(BinaryWriter bw, T val) where T : unmanaged
        {
            bw.Write(MemoryMarshal.Cast<T, byte>(MemoryMarshal.CreateSpan(ref val, 1)));
        }

        protected static T DeserializeV<T>(BinaryReader br) where T : unmanaged
        {
            T val = default;
            br.Read(MemoryMarshal.Cast<T, byte>(MemoryMarshal.CreateSpan(ref val, 1)));
            return val;
        }

        protected static void SerializeO(BinaryWriter bw, SyntaxNode val)
        {
            if (val is null)
            {
                bw.Write(false);
            }
            else
            {
                bw.Write(true);
                val.Serialize(bw);
            }
        }

        protected static T DeserializeO<T>(BinaryReader br) where T : SyntaxNode
        {
            if (!br.ReadBoolean())
            {
                return null;
            }
            else
            {
                var obj = ToType<SyntaxNode>(br.ReadInt32());
                obj.Deserialize(br);
                return (T)obj;
            }
        }

        protected static void SerializeL<T>(BinaryWriter bw, List<T> list) where T : SyntaxNode
        {
            bw.Write(list.Count);
            for (int i = 0; i < list.Count; ++i)
            {
                SerializeO(bw, list[i]);
            }
        }

        protected static void DeserializeL<T>(BinaryReader br, List<T> list) where T : SyntaxNode
        {
            var count = br.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                list.Add(DeserializeO<T>(br));
            }
        }

        protected static void SerializeR<T>(BinaryWriter bw, NodeRef<T> val) where T : SyntaxNode
        {
            if (val is null)
            {
                bw.Write(false);
            }
            else
            {
                bw.Write(true);
                bw.Write(val.TargetId);
            }
        }

        protected static NodeRef<T> DeserializeR<T>(BinaryReader br) where T : SyntaxNode
        {
            if (!br.ReadBoolean())
            {
                return null;
            }
            else
            {
                return new(br.ReadUInt64());
            }
        }

        protected static void SerializeRL<T>(BinaryWriter bw, List<NodeRef<T>> list) where T : SyntaxNode
        {
            bw.Write(list.Count);
            for (int i = 0; i < list.Count; ++i)
            {
                SerializeR(bw, list[i]);
            }
        }

        protected static void DeserializeRL<T>(BinaryReader br, List<NodeRef<T>> list) where T : SyntaxNode
        {
            var count = br.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                list.Add(DeserializeR<T>(br));
            }
        }
    }
}
