# FastLua

FastLua is a Lua 5.2 implementation in pure C#, running on .NET 5.

There are already some popular Lua implementations in C#, including
MoonSharp and KopiLua. However, I found these implementations too slow
when I was testing [ScriptBlazor](https://github.com/acaly/ScriptBlazor).

[Github Actions](https://github.com/acaly/FastLua/actions) contains a
benchmark that tracks the performance of FastLua compared with the other
two implementations.

## FAQ

__Q: So, how fast is it?__ <br>
A: Currently 2-4 times faster than the other two implementations. Expect
much more than this once the JIT (to CIL) is implemented.

__Q: What kind of API does it expose?__ <br>
A: It will not have similar API as original Lua implementation. This is
mainly because it uses .NET GC, and the interop between C# and Lua is
different from C. The API will be more C#-ish. For example, use custom
awaitable for coroutines.

__Q: Will it support older versions of the runtime?__ <br>
A: No. It intensively uses new features such as ref return and `Span`.
While it might be possible to run under .NET Standard 2.1, it won't be
easy to make it run on older runtimes, including Unity.

## Current status

Basic functionality:
- [x] Call Lua from C#.
- [ ] Call C# from Lua.
- [ ] Metatable.
- [ ] Exception handling.
- [ ] Coroutine.

Optimizations:
- [ ] AST optimizations.
- [ ] Instruction specialization.
- [ ] JIT with System.Reflection.Emit.

Standard library:
- [ ] Basic.
- [ ] String.
- [ ] Table.
- [ ] Math.
- [ ] Bitwise.
- [ ] IO.
- [ ] OS.

Features that will not be supported:
- [ ] Modules library.
- [ ] Weak table (ephemeron is not supported by .NET).
- [ ] Lua Debug Interface.
