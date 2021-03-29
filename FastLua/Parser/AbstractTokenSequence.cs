using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.Parser
{
    public struct GenericTokenStorage<TTokenType>
    {
        public ArraySegment<char> Content;
        public TTokenType Type;

        public bool Split(int pos, out GenericTokenStorage<TTokenType> newToken, ArrayPool<char> pool)
        {
            if (pos == 0 || pos == Content.Count)
            {
                newToken = default;
                return false;
            }
            var first = Content[..pos];
            var last = Content[pos..];
            var lastCopy = pool.Rent(last.Count);
            last.CopyTo(lastCopy);
            this = new() { Content = first, Type = Type };
            newToken = new() { Content = new(lastCopy, 0, last.Count), Type = Type };
            return true;
        }
    }

    //Generic base class of all token sequences. Provide basic functionality of peek/split.
    //Note that the default value of TTokenStorage is used as EOS before peek limit.
    public abstract class AbstractTokenSequence<TTokenType, TTokenStorage> : IDetachableTokenSequence<TTokenType>
        where TTokenType : unmanaged
    {
        private bool _isInited = false;
        public bool IsAttached { get; protected set; } = true;
        private int? _peekLimit = null;
        protected readonly List<TTokenStorage> BackwardList = new();

        protected abstract void ReadFirstToken();
        protected abstract bool TryMoveToNextToken(out TTokenStorage lastAsStorage);
        protected abstract GenericToken<TTokenType> GetCurrentToken();
        protected abstract GenericToken<TTokenType> ConvertToken(TTokenStorage storage);
        protected abstract bool SplitStorage(int pos, ref TTokenStorage originalToken, out TTokenStorage newToken);

        protected abstract void ConsumeToken(TTokenStorage storage);
        protected abstract void CancelToken(TTokenStorage storage, bool isLast);

        protected virtual void Reset()
        {
            DetachInput();
            _isInited = false;
        }

        public virtual void AttachInput()
        {
            if (IsAttached)
            {
                throw new InvalidOperationException();
            }
            IsAttached = true;
            ReadFirstToken();
        }

        public virtual void DetachInput()
        {
            if (!IsAttached)
            {
                throw new InvalidOperationException();
            }
            if (_peekLimit.HasValue)
            {
                throw new InvalidOperationException();
            }
            IsAttached = false;
            for (int i = BackwardList.Count - 1; i >= 0; --i)
            {
                CancelToken(BackwardList[i], i == 0);
                BackwardList.RemoveAt(i);
            }
        }

        public GenericToken<TTokenType> Current
        {
            get
            {
                if (!IsAttached)
                {
                    throw new InvalidOperationException();
                }
                if (BackwardList.Count > 0)
                {
                    return ConvertToken(BackwardList[0]);
                }
                return GetCurrentToken();
            }
        }

        public bool MoveNext()
        {
            if (!IsAttached)
            {
                throw new InvalidOperationException();
            }
            if (!_isInited)
            {
                ReadFirstToken();
                _isInited = true;
                return true;
            }
            if (_peekLimit == 0)
            {
                return false;
            }
            if (_peekLimit == 1)
            {
                //Insert the limited EOS.
                BackwardList.Insert(0, default);
                _peekLimit -= 1;
                return true;
            }
            if (BackwardList.Count > 0)
            {
                ConsumeToken(BackwardList[0]);
                BackwardList.RemoveAt(0);
                _peekLimit -= 1;
                return true;
            }
            Debug.Assert(!_peekLimit.HasValue);
            if (TryMoveToNextToken(out var consumed))
            {
                ConsumeToken(consumed);
                return true;
            }
            return false;
        }

        public bool TryPeek(int distance, out GenericToken<TTokenType> value)
        {
            if (!_isInited)
            {
                ReadFirstToken();
                _isInited = true;
            }
            if (distance > _peekLimit)
            {
                value = default;
                return false;
            }
            else if (distance == _peekLimit)
            {
                //Return limited EOS.
                value = ConvertToken(default);
                return true;
            }

            //If it's the one next to the Current, move Current to backward list and continue.
            if (distance == BackwardList.Count + 1)
            {
                if (!TryMoveToNextToken(out var last))
                {
                    value = default;
                    return false;
                }
                BackwardList.Add(last);
            }
            if (distance == BackwardList.Count)
            {
                value = GetCurrentToken();
                return true;
            }
            else if (distance < BackwardList.Count)
            {
                value = ConvertToken(BackwardList[distance]);
                return true;
            }
            throw new ArgumentOutOfRangeException(nameof(distance));
        }

        public void Split(int distance, int position)
        {
            if (position < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }
            if (distance >= _peekLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(distance));
            }
            if (distance > BackwardList.Count)
            {
                //The requested token is not in _backwardBuffer or Current.
                //How do you know where to split?
                throw new ArgumentOutOfRangeException(nameof(distance));
            }
            if (distance == BackwardList.Count)
            {
                if (!TryMoveToNextToken(out var last))
                {
                    //The last token (should be EOS) is not allowed to split.
                    throw new InvalidOperationException();
                }
                BackwardList.Add(last);
            }
            var token = BackwardList[distance];
            if (SplitStorage(position, ref token, out var newToken))
            {
                BackwardList.Insert(distance + 1, newToken);
            }
            BackwardList[distance] = token;
        }

        public void SetPeekLimit(int firstDisallowedDistance)
        {
            if (!IsAttached)
            {
                throw new InvalidOperationException();
            }
            if (_peekLimit.HasValue)
            {
                throw new InvalidOperationException();
            }
            if (firstDisallowedDistance < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(firstDisallowedDistance));
            }
            _peekLimit = firstDisallowedDistance;
        }

        public void ClearPeekLimit(bool ignoreCurrentPos)
        {
            if (!IsAttached)
            {
                throw new InvalidOperationException();
            }
            if (_peekLimit != 0)
            {
                if (!ignoreCurrentPos)
                {
                    throw new InvalidOperationException();
                }
            }
            else
            {
                //Limited EOS has been reached already. We need to remove the default storage.
                BackwardList.RemoveAt(0);
            }
            _peekLimit = null;
        }
    }
}
