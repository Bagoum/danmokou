using System;
using System.Collections.Generic;
using System.Reflection;
using LanguageExt;
using LanguageExt.Parsec;
using static LanguageExt.Prelude;
using static LanguageExt.Parsec.Prim;
using static LanguageExt.Parsec.Char;
using static LanguageExt.Parsec.Expr;
using static LanguageExt.Parsec.Token;

namespace ParserCS {
public static class Common {
    public readonly struct Maybe<T> {
        private readonly T value;
        private readonly bool isValid;

        public Maybe(T value) {
            this.value = value;
            this.isValid = true;
        }

        private Maybe(T value, bool valid) {
            this.value = value;
            this.isValid = valid;
        }

        public static implicit operator Maybe<T>(T obj) => new Maybe<T>(obj);
        public static readonly Maybe<T> Null = new Maybe<T>(default!, false);

        public bool Try(out T val) {
            if (isValid) {
                val = value;
                return true;
            } else {
                val = default!;
                return false;
            }
        }
    }
    
    public const char OPEN_ARG = '(';
    public const char CLOSE_ARG = ')';
    public const char ARG_SEP = ',';
    public const char NEWLINE = '\n';


    public static Parser<T> BetweenChars<T>(char c1, char c2, Parser<T> p) =>
        Sequential(ch(c1), p, ch(c2), (_, x, __) => x);

    public static Parser<T> BetweenStrs<T>(string c1, string c2, Parser<T> p) =>
        Sequential(PString(c1), p, PString(c2), (_, x, __) => x);

    public static Parser<string> Bounded(char c) =>
        BetweenChars(c, c, manyString(satisfy(x => x != c)));

    public static bool WhiteInline(char c) => c != NEWLINE && char.IsWhiteSpace(c);


    public static Parser<List<T>> Paren<T>(Parser<T> p) => Paren1(
        Sequential(spaces, 
            sepBy(p, Sequential(
                spaces, 
                ch(ARG_SEP), 
                spaces, (_, __, ___) => unit)
        ), (_, x) => x));

    public static Parser<T> Paren1<T>(Parser<T> p) => BetweenChars(OPEN_ARG, CLOSE_ARG, p);

    public static Parser<Unit> ILSpaces = skipMany(satisfy(WhiteInline));
    public static Parser<Unit> ILSpaces1 = skipMany1(satisfy(WhiteInline));
    public static Parser<Unit> Spaces1 = skipMany1(space);
}
}