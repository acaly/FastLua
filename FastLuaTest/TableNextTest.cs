using FastLua.VM;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLuaTest
{
    public class TableNextTest
    {
        private static void TestNext(TypedValue[] keys, TypedValue[] removedKeys)
        {
            var table = new Table();

            //Write.
            foreach (var k in keys)
            {
                table.Set(k, k);
            }
            foreach (var k in removedKeys)
            {
                table.Set(k, TypedValue.Nil);
            }

            //Read.
            HashSet<TypedValue> enumeratedKeys = new();
            TypedValue e = TypedValue.Nil;
            while (true)
            {
                table.Next(ref e, out var val);
                if (e.Type == VMSpecializationType.Nil) break;
                Assert.AreEqual(e, val);
                enumeratedKeys.Add(e);
            }

            Assert.AreEqual(keys.Length - removedKeys.Length, enumeratedKeys.Count);
            Assert.True(keys.Except(removedKeys).All(k => enumeratedKeys.Contains(k)));
        }

        private static int GetBucketSize(int count)
        {
            var t = new Table();
            for (int i = 0; i < count; ++i)
            {
                t.SetRaw(TypedValue.MakeString(i.ToString()), TypedValue.True);
            }
            return t.BucketSize;
        }

        private static TypedValue[] MakeCollisionKeys(int sameCount, int diffCount)
        {
            List<TypedValue> retSame = new(), retDiff = new();

            var bucketSize = GetBucketSize(sameCount + diffCount);

            var zero = TypedValue.MakeString((0).ToString());
            var zeroIndex = zero.GetHashCode() % bucketSize;
            retSame.Add(zero);

            int sameAdded = 1, diffAdded = 0;
            int i = 1;
            while (sameAdded < sameCount || diffAdded < diffCount)
            {
                var key = TypedValue.MakeString((i++).ToString());
                var isSame = (key.GetHashCode() % bucketSize) == zeroIndex;
                if (isSame && sameAdded < sameCount)
                {
                    sameAdded += 1;
                    retSame.Add(key);
                }
                else if (!isSame && diffAdded < diffCount)
                {
                    diffAdded += 1;
                    retDiff.Add(key);
                }
            }
            Debug.Assert(retSame.Count == sameCount);
            Debug.Assert(retDiff.Count == diffCount);
            return retSame.Concat(retDiff).ToArray();
        }

        [Test]
        public void Collision30()
        {
            var keys = MakeCollisionKeys(3, 0);
            TestNext(keys, Array.Empty<TypedValue>());
        }

        [Test]
        public void Collision31()
        {
            var keys = MakeCollisionKeys(3, 1);
            TestNext(keys, Array.Empty<TypedValue>());
        }

        [Test]
        public void Collision32()
        {
            var keys = MakeCollisionKeys(3, 2);
            TestNext(keys, Array.Empty<TypedValue>());
        }

        [Test]
        public void Collision14()
        {
            var keys = MakeCollisionKeys(1, 4);
            TestNext(keys, Array.Empty<TypedValue>());
        }

        [Test]
        public void Removed()
        {
            var keys = MakeCollisionKeys(1, 4);
            TestNext(keys, keys.Take(2).ToArray());
        }

        [Test]
        public void RemovedCollision()
        {
            var keys = MakeCollisionKeys(3, 2);
            TestNext(keys, keys.Take(2).ToArray());
        }

        [Test]
        public void RemovedNoneCollision()
        {
            var keys = MakeCollisionKeys(3, 2);
            TestNext(keys, keys.Skip(3).ToArray());
        }

        [Test]
        public void Large()
        {
            var keys = MakeCollisionKeys(10, 30);
            TestNext(keys, Array.Empty<TypedValue>());
        }

        [Test]
        public void LargeRemove()
        {
            var keys = MakeCollisionKeys(10, 30);
            TestNext(keys, keys.Take(3).Concat(keys.Skip(10).Take(3)).ToArray());
        }
    }
}
