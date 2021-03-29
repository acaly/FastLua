using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.Parser
{
    public interface ITokenSequence<TTokenType>
        where TTokenType : unmanaged
    {
        GenericToken<TTokenType> Current { get; }
        bool MoveNext();
        bool TryPeek(int distance, out GenericToken<TTokenType> value);
        void Split(int distance, int position);
    }

    public interface IDetachableTokenSequence<TTokenType> : ITokenSequence<TTokenType>
        where TTokenType : unmanaged
    {
        void AttachInput();
        void DetachInput();
    }

    public static class TokenSequenceExtensions
    {
        public static ITokenSequence<TTokenType> GetEnumerator<TTokenType>(this ITokenSequence<TTokenType> s)
            where TTokenType : unmanaged
        {
            return s;
        }

        public static void EnsureMoveNext<TTokenType>(this ITokenSequence<TTokenType> s)
            where TTokenType : unmanaged
        {
            if (!s.MoveNext())
            {
                throw new EndOfStreamException();
            }
        }
    }
}
