using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.Parser
{
    public ref struct GenericToken<TTokenType>
        where TTokenType : unmanaged
    {
        public ReadOnlySpan<char> Content;
        public TTokenType Type;
    }

    public static class GenericTokenExtensions
    {
        public static void Deconstruct<TTokenType>(this GenericToken<TTokenType> t,
            out TTokenType type, out ReadOnlySpan<char> content)
            where TTokenType : unmanaged
        {
            type = t.Type;
            content = t.Content;
        }
    }

    //Definition of the state transition of the tokenizer state machine.
    public delegate bool SplitTokenizerDelegate<TTokenType>(ref TTokenType state, char newChar);

    //This tokenizer split input token into smaller ones based on the selector parameter.
    //Specifically, it split whenever the next char is of different type.
    //TODO we need another version of SplitTokenSequence to accept a ITokenSequence to
    //make a chained input, which allows sharing input with another, e.g., HTML or json parser.
    public class SplitTokenSequence<TTokenType> : AbstractTokenSequence<TTokenType, GenericTokenStorage<TTokenType>>
        where TTokenType : unmanaged
    {
        private TextReader _input;
        private readonly SplitTokenizerDelegate<TTokenType> _selector;
        private readonly ArrayPool<char> _contentPool = ArrayPool<char>.Create(100, 5);
        private readonly TTokenType _eosToken;

        private CharBuffer<char> _buffer;
        private TTokenType _type;
        private bool _eos;

        public SplitTokenSequence(SplitTokenizerDelegate<TTokenType> selector, TTokenType eos)
        {
            _selector = selector;
            _eosToken = eos;
        }

        public void Reset(TextReader input)
        {
            Reset();
            _input = input;

            _buffer.Clear();
            _type = default;
            _eos = false;
            IsAttached = true;
        }

        //Should not touch _buffer or _type if failed.
        private bool ReadInternal()
        {
            var next = _input.Peek();
            if (next <= 0)
            {
                if (!_eos)
                {
                    _eos = true;
                    _buffer.Clear();
                    _type = _eosToken;
                    return true;
                }
                return false;
            }

            //Initialize _buffer and _type.
            TTokenType type = default;
            _buffer.Clear();
            _type = default;
            _selector(ref type, (char)next);

            //Loop.
            do
            {
                _type = type;
                _buffer.Write((char)next);
                _input.Read();
                next = _input.Peek();
            } while (next > 0 && _selector(ref type, (char)next));

            return true;
        }

        protected override void ReadFirstToken()
        {
            if (!ReadInternal())
            {
                throw new EndOfStreamException();
            }
        }

        protected override bool TryMoveToNextToken(out GenericTokenStorage<TTokenType> lastAsStorage)
        {
            var lastContent = _contentPool.Rent(_buffer.Content.Length);
            var lastLength = _buffer.Content.Length;
            _buffer.Content.CopyTo(lastContent);
            var type = _type;
            if (ReadInternal())
            {
                lastAsStorage = new() { Content = new(lastContent, 0, lastLength), Type = type };
                return true;
            }
            else
            {
                _contentPool.Return(lastContent);
                lastAsStorage = default;
                return false;
            }
        }

        protected override GenericToken<TTokenType> GetCurrentToken()
        {
            return new() { Content = _buffer.Content, Type = _type };
        }

        protected override GenericToken<TTokenType> ConvertToken(GenericTokenStorage<TTokenType> storage)
        {
            return new() { Content = storage.Content, Type = storage.Type };
        }

        protected override bool SplitStorage(int pos,
            ref GenericTokenStorage<TTokenType> originalToken, out GenericTokenStorage<TTokenType> newToken)
        {
            return originalToken.Split(pos, out newToken, _contentPool);
        }

        protected override void ConsumeToken(GenericTokenStorage<TTokenType> storage)
        {
            _contentPool.Return(storage.Content.Array);
        }

        protected override void CancelToken(GenericTokenStorage<TTokenType> storage, bool isLast)
        {
            if (IsAttached)
            {
                //Cannot go backward on a TextReader.
                throw new NotSupportedException();
            }
            _contentPool.Return(storage.Content.Array);
        }
    }
}
