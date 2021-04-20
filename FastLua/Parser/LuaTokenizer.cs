using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLua.Parser
{
    public enum LuaRawTokenType
    {
        Invalid,
        EOS,
        Whitespace,
        Symbols,
        Text,
    }

    public enum LuaTokenType
    {
        EOS = 0,

        //We are using UTF16, so starting from 65536 instead of 256.
        First = 65536,
        And, Break,
        Do, Else, Elseif, End, False, For, Function,
        Goto, If, In, Local, Nil, Not, Or, Repeat,
        Return, Then, True, Until, While,

        Concat, Dots, Eq, Ge, Le, Ne, Dbcolon, Eos,
        Number, Name, String,
    }

    public static class LuaTokenTypeExtensions
    {
        public static string ToReadableString(this LuaTokenType t)
        {
            if (t == LuaTokenType.EOS)
            {
                return "EOS";
            }
            if (t < LuaTokenType.First)
            {
                return new string((char)t, 1);
            }
            return t.ToString();
        }
    }

    public class LuaTokenizer : AbstractTokenSequence<LuaTokenType, LuaTokenizer.Storage>
    {
        private static readonly StringDictionary<LuaTokenType> _keywords = new()
        {
            { "do", LuaTokenType.Do },
            { "else", LuaTokenType.Else },
            { "elseif", LuaTokenType.Elseif },
            { "end", LuaTokenType.End },
            { "false", LuaTokenType.False },
            { "for", LuaTokenType.For },
            { "function", LuaTokenType.Function },
            { "goto", LuaTokenType.Goto },
            { "if", LuaTokenType.If },
            { "in", LuaTokenType.In },
            { "local", LuaTokenType.Local },
            { "nil", LuaTokenType.Nil },
            { "not", LuaTokenType.Not },
            { "or", LuaTokenType.Or },
            { "repeat", LuaTokenType.Repeat },
            { "return", LuaTokenType.Return },
            { "then", LuaTokenType.Then },
            { "true", LuaTokenType.True },
            { "until", LuaTokenType.Until },
            { "while", LuaTokenType.While },
        };

        public struct Storage
        {
            public GenericTokenStorage<LuaTokenType> Data;
            public int CommentLen;
            public int TokenLen;
        }

        private ITokenSequence<LuaRawTokenType> _input;
        private readonly ArrayPool<char> _contentPool = ArrayPool<char>.Create(100, 5);

        private CharBuffer<char> _buffer;
        private LuaTokenType _type;
        private int _commentLen, _tokenLen;

        private int _totalPeekLength;
        private char _nextChar;
        private LuaRawTokenType _nextCharType;

        public void Reset(ITokenSequence<LuaRawTokenType> input)
        {
            Reset();
            _input = input;

            _buffer.Clear();
            _type = (LuaTokenType)(-1);
            _commentLen = _tokenLen = 0;
            _totalPeekLength = 0;
            _nextChar = default;
            _nextCharType = LuaRawTokenType.Invalid;
            IsAttached = true;
        }

        private char PeekChar(int distance)
        {
            return PeekCharInternal(_totalPeekLength + distance).c;
        }

        private void Forward(int count)
        {
            _totalPeekLength += count;
            _tokenLen += count;
        }

        private char NextChar()
        {
            Forward(1);
            (_nextChar, _nextCharType) = PeekCharInternal(_totalPeekLength);
            return _nextChar;
        }

        private (char c, LuaRawTokenType t) PeekCharInternal(int distance)
        {
            int peek = 0;
            while (_input.TryPeek(peek, out var inputToken))
            {
                if (inputToken.Content.Length > distance)
                {
                    return (inputToken.Content[distance], inputToken.Type);
                }
                distance -= inputToken.Content.Length;
                peek += 1;
            }
            return (default, LuaRawTokenType.EOS);
        }

        private void CopyAllText()
        {
            int peek = 0;
            int pos = _totalPeekLength;
            while (_input.TryPeek(peek, out var inputToken))
            {
                if (inputToken.Content.Length > pos)
                {
                    //The first token is ensured to be Text.
                    Debug.Assert(inputToken.Type == LuaRawTokenType.Text);

                    _buffer.Write(inputToken.Content[pos..]);
                    Forward(inputToken.Content.Length - pos);

                    while (_input.TryPeek(++peek, out inputToken) &&
                        inputToken.Type == LuaRawTokenType.Text)
                    {
                        _buffer.Write(inputToken.Content);
                        Forward(inputToken.Content.Length);
                    }

                    (_nextChar, _nextCharType) = PeekCharInternal(_totalPeekLength);
                    return;
                }
                pos -= inputToken.Content.Length;
                peek += 1;
            }
        }

        private void MarkComment()
        {
            _commentLen += _tokenLen; //Set start position.
            _tokenLen = 0;
        }

        private bool MakeToken(char c)
        {
            _type = (LuaTokenType)c;
            return true;
        }

        private bool MakeToken(LuaTokenType t)
        {
            _type = t;
            return true;
        }

        private bool ReadInternal()
        {
            if (_nextCharType == LuaRawTokenType.EOS)
            {
                if (_type != LuaTokenType.EOS)
                {
                    _buffer.Clear();
                    _type = LuaTokenType.EOS;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            _commentLen = _tokenLen = 0;
            _buffer.Clear();
            _type = default;

            while (true)
            {
                switch (_nextChar)
                {
                case '\n':
                case '\r':
                case ' ':
                case '\f':
                case '\t':
                case '\v':
                {
                    NextChar();
                    break;
                }
                case '-': //'-' or '--'
                {
                    if (NextChar() != '-')
                    {
                        //Not a comment.
                        return MakeToken('-');
                    }
                    if (NextChar() == '[')
                    {
                        var sep = SkipSep(null);
                        if (sep >= 0)
                        {
                            //Long comment.
                            ReadLongString(sep);
                            _buffer.Clear(); //Discard string.
                            MarkComment();
                            break;
                        }
                    }
                    //Short comment.
                    while (_nextChar != '\r' && _nextChar != '\n' && _nextChar != default)
                    {
                        NextChar();
                    }
                    MarkComment();
                    break;
                }
                case '[': //long string or '['
                {
                    var sep = SkipSep(null);
                    _buffer.Clear(); //SkipSep will write to buffer.
                    if (sep >= 0)
                    {
                        ReadLongString(sep);
                        //Long strings are not escaped.
                        return MakeToken(LuaTokenType.String);
                    }
                    else if (sep == -1)
                    {
                        return MakeToken('[');
                    }
                    else
                    {
                        throw new Exception("Invalid long string delimiter");
                    }
                }
                case '=': //'=' or eq
                {
                    if (NextChar() != '=')
                    {
                        return MakeToken('=');
                    }
                    NextChar();
                    return MakeToken(LuaTokenType.Eq);
                }
                case '<': //'<' or le
                {
                    if (NextChar() != '=')
                    {
                        return MakeToken('<');
                    }
                    NextChar();
                    return MakeToken(LuaTokenType.Le);
                }
                case '>': //'>' or ge
                {
                    if (NextChar() != '=')
                    {
                        return MakeToken('>');
                    }
                    NextChar();
                    return MakeToken(LuaTokenType.Ge);
                }
                case '~': //'~' or ne
                {
                    if (NextChar() != '=')
                    {
                        return MakeToken('~');
                    }
                    NextChar();
                    return MakeToken(LuaTokenType.Ne);
                }
                case ':': //':' or dbcolon
                {
                    if (NextChar() != ':')
                    {
                        return MakeToken(':');
                    }
                    NextChar();
                    return MakeToken(LuaTokenType.Dbcolon);
                }
                case '"': //short literal string
                case '\'':
                {
                    ReadString();
                    EscapeStringBuffer();
                    return MakeToken(LuaTokenType.String);
                }
                case '.': //'.', concat, dots or number
                {
                    var next = PeekChar(1);
                    if (next == '.')
                    {
                        //Not a number.
                        NextChar(); //Skip the second '.'.
                        if (NextChar() != '.')
                        {
                            return MakeToken(LuaTokenType.Concat);
                        }
                        NextChar(); //Skip the third '.'.
                        return MakeToken(LuaTokenType.Dots);
                    }
                    else if (next >= '0' && next <= '9')
                    {
                        ReadNumeral();
                        return MakeToken(LuaTokenType.Number);
                    }
                    else
                    {
                        NextChar();
                        return MakeToken('.');
                    }
                }
                case >= '0' and <= '9': //number
                {
                    ReadNumeral();
                    return MakeToken(LuaTokenType.Number);
                }
                case default(char):
                    return MakeToken(LuaTokenType.EOS);
                default:
                    if (_nextCharType == LuaRawTokenType.Text)
                    {
                        //Identifier or keyword.
                        ReadIdentifier();
                        if (_keywords.TryGetValue(_buffer.Content, out var keyword))
                        {
                            _buffer.Clear();
                            return MakeToken(keyword);
                        }
                        return MakeToken(LuaTokenType.Name);
                    }
                    else
                    {
                        //Single char token.
                        var retChar = _nextChar;
                        NextChar();
                        return MakeToken(retChar);
                    }
                }
            }
        }

        //Behavior:
        //If it's a valid sep, skip the sep.
        //If it's a "[" (or "]") (or an invalid sep), only skip the '[' (or ']').
        //Only write to buffer when expectedCount is provided but is different from count.
        private int SkipSep(int? expectedCount)
        {
            var s = _nextChar; //'[' or ']'.
            NextChar(); //Skip s.
            int count = 0;

            for (; PeekChar(count) == '='; ++count)
            {
            }

            if (PeekChar(count) == s)
            {
                //Valid sep. Consume from input and write to output.
                if (count != expectedCount)
                {
                    _buffer.Write(s); //s has been skipped. Just write.
                    for (int i = 0; i < count; ++i)
                    {
                        _buffer.Write('=');
                    }
                    _buffer.Write(s);
                }
                for (int i = 0; i < count + 1; ++i)
                {
                    NextChar();
                }

                return count;
            }

            //Invalid sep.
            if (expectedCount.HasValue)
            {
                //This is in a long string. Since we have skipped s, write it to be consistent.
                _buffer.Write(s);
            }
            return -count - 1; //Return -1 means a single [ without =.
        }

        //Starting sep has been skipped (this is different from original Lua impl).
        //Note that we don't handle escape sequences here. It's done later.
        private void ReadLongString(int sep)
        {
            while (true)
            {
                switch (_nextChar)
                {
                case default(char):
                    throw new Exception("Unfinished long string or comment");
                case ']':
                    if (SkipSep(sep) == sep)
                    {
                        //Success.
                        return;
                    }
                    //Only ']' is consumed (and written to _buffer).
                    break;
                default:
                    _buffer.Write(_nextChar);
                    NextChar();
                    break;
                }
            }
        }

        private void ReadString()
        {
            var s = _nextChar;
            NextChar(); //Skip ' or ".
            while (_nextChar != s)
            {
                if (_nextChar == default)
                {
                    throw new Exception("Invalid string literal");
                }
                _buffer.Write(_nextChar);
                if (_nextChar == '\\' && PeekChar(1) == s)
                {
                    //Write as an escape sequence (handled later in EscapeStringBuffer).
                    NextChar();
                    _buffer.Write(_nextChar);
                }
                NextChar();
            }
            NextChar(); //Skip ' or " at the end.
        }

        private void EscapeStringBuffer()
        {
            for (int i = 0; i < _buffer.Content.Length - 1; ++i)
            {
                if (_buffer.Content[i] == '\\')
                {
                    switch (_buffer.Content[i + 1])
                    {
                    case 'a':
                        _buffer.WritableContent[i] = '\a';
                        _buffer.RemoveAt(i + 1);
                        break;
                    case 'b':
                        _buffer.WritableContent[i] = '\b';
                        _buffer.RemoveAt(i + 1);
                        break;
                    case 'f':
                        _buffer.WritableContent[i] = '\f';
                        _buffer.RemoveAt(i + 1);
                        break;
                    case 'n':
                        _buffer.WritableContent[i] = '\n';
                        _buffer.RemoveAt(i + 1);
                        break;
                    case 'r':
                        _buffer.WritableContent[i] = '\r';
                        _buffer.RemoveAt(i + 1);
                        break;
                    case 't':
                        _buffer.WritableContent[i] = '\t';
                        _buffer.RemoveAt(i + 1);
                        break;
                    case 'v':
                        _buffer.WritableContent[i] = '\v';
                        _buffer.RemoveAt(i + 1);
                        break;
                    case '\\':
                        _buffer.WritableContent[i] = '\\';
                        _buffer.RemoveAt(i + 1);
                        break;
                    case '"':
                        _buffer.WritableContent[i] = '"';
                        _buffer.RemoveAt(i + 1);
                        break;
                    case '\'':
                        _buffer.WritableContent[i] = '\'';
                        _buffer.RemoveAt(i + 1);
                        break;
                    case '\r':
                    case '\n':
                        _buffer.RemoveAt(i); //Remove the '\', keep the newline.
                        break;
                    case 'z':
                    {
                        _buffer.RemoveAt(i + 1);
                        _buffer.RemoveAt(i);
                        char cc = _buffer.Content[i];
                        while (char.IsWhiteSpace(cc) || char.IsSeparator(cc) || char.IsControl(cc))
                        {
                            _buffer.RemoveAt(i);
                            cc = _buffer.Content[i];
                        }
                        break;
                    }
                    case 'x':
                    {
                        if (i + 3 >= _buffer.Content.Length)
                        {
                            throw new LuaCompilerException("Invalid heximal escape.");
                        }
                        int X(char c) => c >= 'a' ? c - 'a' + 10 : c >= 'A' ? c - 'A' + 10 : c - '0';
                        var x1 = X(_buffer.Content[i + 2]);
                        var x2 = X(_buffer.Content[i + 3]);
                        if (x1 < 0 || x1 > 15 || x2 < 0 || x2 > 15)
                        {
                            throw new LuaCompilerException("Invalid heximal escape.");
                        }
                        _buffer.WritableContent[i] = (char)(x1 << 8 | x2);
                        //Can be a remove range, but this should be rare enough.
                        _buffer.RemoveAt(i + 3);
                        _buffer.RemoveAt(i + 2);
                        _buffer.RemoveAt(i + 1);
                        break;
                    }
                    case >= '0' and <= '9':
                    {
                        if (i + 4 >= _buffer.Content.Length)
                        {
                            if (_buffer.Content[i + 1] == '0')
                            {
                                _buffer.WritableContent[i] = '\0';
                                _buffer.RemoveAt(i + 1);
                            }
                            else
                            {
                                throw new LuaCompilerException("Invalid decimal escape.");
                            }
                        }
                        else
                        {
                            int x1 = _buffer.Content[i + 1], x2 = _buffer.Content[i + 2], x3 = _buffer.Content[i + 3];
                            if (x1 < 0 || x1 > 9 ||
                                x2 < 0 || x2 > 9 ||
                                x3 < 0 || x3 > 9)
                            {
                                throw new LuaCompilerException("Invalid heximal escape.");
                            }
                            var x = x1 * 100 + x2 * 10 + x3;
                            if (x > 255)
                            {
                                throw new LuaCompilerException("Invalid heximal escape.");
                            }
                            _buffer.WritableContent[i] = (char)x;
                            _buffer.RemoveAt(i + 3);
                            _buffer.RemoveAt(i + 2);
                            _buffer.RemoveAt(i + 1);
                        }
                        break;
                    }
                    //TODO we are using utf16 chars. Should add unicode escape.
                    default:
                        break;
                    }
                }
            }
        }

        private void ReadNumeral()
        {
            //Skip 0x.
            var nextChar = PeekChar(1);
            var useBinaryExp = _nextChar == '0' && nextChar == 'x' || nextChar == 'X';
            if (useBinaryExp)
            {
                _buffer.Write(_nextChar);
                NextChar();
                _buffer.Write(_nextChar);
                NextChar();
            }

            char FastToLower(char c) //Don't go into CultureInfo.
            {
                if (c >= 'A' && c <= 'Z')
                {
                    return (char)('a' + (c - 'A'));
                }
                return c;
            }
            bool IsNumeralChar(char c)
            {
                //Lua allows decimal point in current culture. We don't allow it here for simplicity.
                return c >= '0' && c <= '9' ||
                    c == '+' || c == '-' || c == '.' ||
                    FastToLower(c) == (useBinaryExp ? 'p' : 'e');
            }
            while (IsNumeralChar(_nextChar))
            {
                _buffer.Write(_nextChar);
                NextChar();
            }
        }

        private void ReadIdentifier()
        {
            CopyAllText();
        }

        #region AbstractTokenSequence

        protected override void ReadFirstToken()
        {
            //Reset to input's current position after re-attaching.
            _totalPeekLength = 0;
            (_nextChar, _nextCharType) = PeekCharInternal(0);
            if (!ReadInternal())
            {
                throw new EndOfStreamException();
            }
        }

        protected override bool TryMoveToNextToken(out Storage lastAsStorage)
        {
            var lastLength = _buffer.Content.Length;
            var lastContent = _contentPool.Rent(lastLength);
            _buffer.Content.CopyTo(lastContent);
            var s = new Storage
            {
                Data = new() { Type = _type, Content = new(lastContent, 0, lastLength) },
                CommentLen = _commentLen,
                TokenLen = _tokenLen,
            };
            if (ReadInternal())
            {
                lastAsStorage = s;
                return true;
            }
            else
            {
                _contentPool.Return(lastContent);
                lastAsStorage = default;
                return false;
            }
        }

        protected override GenericToken<LuaTokenType> GetCurrentToken()
        {
            return new() { Type = _type, Content = _buffer.Content };
        }

        protected override GenericToken<LuaTokenType> ConvertToken(Storage storage)
        {
            return new() { Type = storage.Data.Type, Content = storage.Data.Content };
        }

        protected override bool SplitStorage(int pos, ref Storage originalToken, out Storage newToken)
        {
            //Never used as input. You cannot split a Lua token.
            throw new NotSupportedException();
        }

        protected override void ConsumeToken(Storage storage)
        {
            _totalPeekLength -= storage.CommentLen;
            ConsumeLuaComments(storage.CommentLen);
            _totalPeekLength -= storage.TokenLen;
            ConsumeLuaToken(storage.TokenLen);
            _contentPool.Return(storage.Data.Content.Array);
        }

        protected override void CancelToken(Storage storage, bool isLast)
        {
            _totalPeekLength -= (_commentLen + _tokenLen);
            _buffer.Clear();
            if (isLast)
            {
                //Only need to write if it's the last in the canceled sequence.
                _buffer.Write(storage.Data.Content);
            }
            _type = storage.Data.Type;
            _commentLen = storage.CommentLen;
            _tokenLen = storage.TokenLen;
            _contentPool.Return(storage.Data.Content.Array);
        }

        protected virtual void ConsumeLuaComments(int length)
        {
            Skip(length);
        }

        protected virtual void ConsumeLuaToken(int length)
        {
            Skip(length);
        }

        protected void Skip(int length)
        {
            while (length > 0)
            {
                var input = _input.Current;
                if (input.Content.Length >= length)
                {
                    _input.Split(0, length);
                    _input.EnsureMoveNext();
                    return;
                }
                length -= input.Content.Length;
                _input.EnsureMoveNext();
            }
        }

        #endregion
    }
}
