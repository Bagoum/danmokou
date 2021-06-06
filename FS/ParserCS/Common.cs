using System;
using System.Collections.Generic;
using System.Reflection;
using LanguageExt;
using LanguageExt.Parsec;
using static LanguageExt.Prelude;
using static LanguageExt.Parsec.Prim;
using static LanguageExt.Parsec.Char;

namespace ParserCS {
public static class Common {
    
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