﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Functional;
using JetBrains.Annotations;
using Mizuhashi;
using Mizuhashi.Lexers;
using UnityEngine;
using static Mizuhashi.Combinators;

namespace Danmokou.Reflection2 {
/// <summary>
/// Lexer for BDSL2.
/// </summary>
public static class Lexer {
    private const string uLetter = @"\p{L}";
    private const string  num = @"[0-9]";
    private const string  numMult = @"pi?|[hfsc]";
    private static readonly HashSet<string> specialOps = new[] { "\\", "->" }.ToHashSet();
    private static readonly HashSet<string> keywords = new[] { "function", "var", "true", "false", "block" }.ToHashSet();
    private static readonly HashSet<string> operators = new[] {
        "++", "--",
        "!", 
        "*", "/", "//", "%", "^",
        "+", "-",
        "<", ">", "<=", ">=", 
        "==", "!=", 
        "&&", "||", "&", "|",
        "=", "+=", "-=", "*=", "/=", "%=", "|=", "&=",
        
        ".", "$", "?", ":", "::",
        //no use planned for these yet, but they are occasionally important in parsing
        "@", "#", "~"
    }.ToHashSet();
    private static readonly Trie operatorTrie = new(operators.Concat(specialOps));

    /// <summary>
    /// A set of flags describing lexical features about the token.
    /// </summary>
    [Flags]
    public enum TokenFlags {
        /// <summary>
        /// This token is preceded by no tokens other than whitespace until the previous newline.
        /// </summary>
        PrecededByNewline = 1 << 0,
        /// <summary>
        /// This token is directly preceded by whitespace or a newline.
        /// </summary>
        PrecededByWhitespace = 1 << 1,
        /// <summary>
        /// This token is directly postceded by whitespace or a newline.
        /// </summary>
        PostcededByWhitespace = 1 << 2,
        /// <summary>
        /// This token is preceded by an implicit semicolon/comma due to indenting layout rules,
        ///  and therefore should not be taken as a partial function application argument.
        /// </summary>
        ImplicitBreak = 1 << 3,
        Default = 0
        
    }

    /// <summary>
    /// The type of the token.
    /// </summary>
    public enum TokenType {
        /// <summary>
        /// Non-newline whitespaces.
        /// </summary>
        InlineWhitespace,
        /// <summary>
        /// Newline whitespace (only \n).
        /// </summary>
        Newline,
        /// <summary>
        /// Comment (either /* block comment */ or  // one-line comment).
        /// </summary>
        Comment,
        /// <summary>
        /// A reserved keyword (that would otherwise be parsed as an identifier).
        /// </summary>
        Keyword,
        /// <summary>
        /// A special operator, such as :: or ->, which has functionality more
        ///  advanced than calling a function.
        /// </summary>
        SpecialOperator,
        /// <summary>
        /// A operator that calls a function, such as + or *.
        /// </summary>
        Operator,
        //special symbols
        // We don't *need* to make these separate from operator, but it's convenient for parsing
        /// <summary>
        /// Open parentheses, (
        /// </summary>
        OpenParen,
        /// <summary>
        /// Close parentheses, )
        /// </summary>
        CloseParen,
        /// <summary>
        /// Open bracket, [
        /// </summary>
        OpenBracket,
        /// <summary>
        /// Close bracket, ]
        /// </summary>
        CloseBracket,
        /// <summary>
        /// Open brace, {
        /// </summary>
        OpenBrace,
        /// <summary>
        /// Close brace, }
        /// </summary>
        CloseBrace,
        Comma,
        Semicolon,
        /// <summary>
        /// An identifier that may be for a variable or method or class or type, etc.
        /// </summary>
        Identifier,
        /// <summary>
        /// An identifier that must be for a method or class or type, and not for a variable.
        /// <br/>Might be generic (eg. List&lt;int&gt;) or array-typed (eg. int[]) or some combination thereof.
        /// </summary>
        TypeIdentifier,
        /// <summary>
        /// A V2RV2 in the format &lt;NX;NY:RX;RY:ANG&gt;.
        /// </summary>
        V2RV2,
        /// <summary>
        /// A number (integer or float). Can be preceded by signs and postceded by numerical multipliers pi, p, h, c, s.
        /// </summary>
        Number,
        /// <summary>
        /// Strings, bounded by " ".
        /// </summary>
        String
    }
    public readonly struct Token {
        public string Content { get; }
        
        /// <summary>
        /// The position of this token in the source string.
        /// </summary>
        public PositionRange Position { get; }

        /// <inheritdoc cref="TokenType"/>
        public TokenType Type { get; }
        
        /// <inheritdoc cref="TokenFlags"/>
        public TokenFlags Flags { get; }
        
        /// <summary>
        /// The starting index of this token in the source string.
        /// </summary>
        public int Index => Position.Start.Index;
        private (TokenType, TokenFlags, string, PositionRange) Tuple => (Type, Flags, Content, Position);

        public Token(TokenType type, Position p, Match m) : this(type, p, m.Value) { }
        
        //flags will be updated in the postprocessor
        public Token(TokenType type, Position p, string content) : this(type, TokenFlags.Default, p, content) { }
        
        public Token(TokenType type, TokenFlags flags, Position p, string content) : 
            this(type, flags, p.CreateRange(content, content.Length), content) { }
        
        public Token(TokenType type, TokenFlags flags, PositionRange p, string content) {
            Type = type;
            Content = content;
            Position = p;
            Flags = flags;
        }

        public Token WithFlags(TokenFlags flags) => new(Type, Flags | flags, Position, Content);
        public Token WithType(TokenType t) => new(t, Flags, Position, Content);

        public override string ToString() => string.IsNullOrWhiteSpace(Content) ? $"({Type})" : $"\"{Content}\" ({Type})";

        public override bool Equals(object? obj) => obj is Token t && this == t;
        public override int GetHashCode() => Tuple.GetHashCode();
        public static bool operator==(Token x, Token y) => x.Tuple == y.Tuple;
        public static bool operator !=(Token x, Token y) => !(x == y);
        
    }
    
    private static RegexTokenizer<Token> T([RegexPattern] string pattern, TokenType t) =>
        new(pattern, (p, m) => (new Token(t, p, m), m.Value.Length));
    private static RegexTokenizer<Token> T([RegexPattern] string pattern, Func<Position, Match, Token> t) =>
        new(pattern, (p, m) => (t(p, m), m.Value.Length));
    
    private static RegexTokenizer<Token> T([RegexPattern] string pattern, Func<Position, Match, Maybe<(Token, int)>>t) =>
        new(pattern, t);
    
    private static readonly RegexLexer<Token> lexer = new(
            T(@"[^\S\n]+", TokenType.InlineWhitespace),
            T($@"{uLetter}({uLetter}|{num}|[_'])*", (p, s) =>
                new Token(keywords.Contains(s.Value) ? TokenType.Keyword : TokenType.Identifier, p, s)),
            //Preprocess out other newlines
            T(@"\n", TokenType.Newline),
            T(@"[\(\)\[\]\{\},;]", (p, s) => new Token(s.Value switch {
                "(" => TokenType.OpenParen,
                ")" => TokenType.CloseParen,
                "[" => TokenType.OpenBracket,
                "]" => TokenType.CloseBracket,
                "{" => TokenType.OpenBrace,
                "}" => TokenType.CloseBrace,
                "," => TokenType.Comma,
                ";" => TokenType.Semicolon,
                _ => throw new ArgumentOutOfRangeException($"Not a special symbol: {s.Value}")
            }, p, s)),
            //123, 123.456, .456
            //Note that the preceding sign is parsed by Operator
            //This is to make basic cases like 5-6 vs 5+ -6 easier to handle
            T($@"(({num}+(\.{num}+)?)|(\.{num}+))({numMult})?", TokenType.Number),
            T(@"[!@#$%^&*+\-.<=>?/\\|~:]+", (p, s) => {
                var op = operatorTrie.FindLongestSubstring(s.Value);
                if (op is null) return Maybe<(Token, int)>.None;
                return (new Token(specialOps.Contains(op) ? TokenType.SpecialOperator : TokenType.Operator, 
                    p, op), op.Length);
            }),
            T(@"""([^""\\]+|\\([a-zA-Z0-9\\""'&]))*""", TokenType.String),
            
            //A block comment "fragment" is either a sequence of *s followed by a not-/ character, or a sequence of not-*s.
            T(@"/\*((\*+[^/])|([^*]+))*\*/", TokenType.Comment),
            T(@"//[^\n]*", TokenType.Comment)
        );

    /// <summary>
    /// Lex the string into a sequence of tokens, discarding whitespace/newlines
    ///  and throwing exceptions if parentheses are unbalanced.
    /// </summary>
    /// <param name="source">Source string</param>
    /// <returns></returns>
    public static Token[] Lex(ref string source) => Lex(ref source, out _);
    
    /// <inheritdoc cref="Lex(ref string)"/>
    public static Token[] Lex(string source) => Lex(ref source, out _);
    
    /// <inheritdoc cref="Lex(ref string)"/>
    public static Token[] Lex(ref string source, out InputStream<Token> initialPostProcess) {
        source = source.Replace("\r\n", "\n").Replace("\r", "\n");
        var tokens = lexer.Tokenize(source);
        //Combinator postprocessing: consolidate structures such as TypeIdentifier and V2RV2
        var stream = initialPostProcess = 
            new InputStream<Token>("Lexer postprocessing", tokens.ToArray(), null!, new TokenWitnessCreator(source));
        var result = postprocessor(stream);
        if (result.Status != ResultStatus.OK)
            throw new Exception(result.ErrorOrThrow.Show(stream));
        tokens = result.Result.Value;
        
        //Manual postprocessing: strip whitespace/NLs, balance parens, and add flags
        var processed = new List<Token>();
        var paren = new GroupingHandler(source, "parenthesis", "parentheses", TokenType.OpenParen,
            TokenType.CloseParen);
        var bracket = new GroupingHandler(source, "bracket", "brackets", TokenType.OpenBracket, TokenType.CloseBracket);
        var brace = new GroupingHandler(source, "brace", "braces", TokenType.OpenBrace, TokenType.CloseBrace);
        bool RequiresWhitespaceSep(TokenType t) => IsAnyIdentifier(t) || t is 
            TokenType.V2RV2 or TokenType.Number or TokenType.String;
        bool IsAnyIdentifier(TokenType t) => t is
            TokenType.Identifier or TokenType.TypeIdentifier;
        bool firstNonWSTokenOnLine = false;
        int? blockIndent = 0; //Indent required to add implicit semicolon/comma
        int indent = 0;
        for (int ii = 0; ii < tokens.Count; ++ii) {
            var t = tokens[ii];
            if (t.Type == TokenType.InlineWhitespace) {
                if (firstNonWSTokenOnLine) {
                    for (int si = 0; si < t.Content.Length; ++si) {
                        if (t.Content[si] == '\t')
                            indent = (indent / 4 + 1) * 4;
                        else
                            ++indent;
                    }
                }
                continue;
            }
            if (t.Type == TokenType.Comment) {
                //Triple slash on newline ends parsing
                if (firstNonWSTokenOnLine && t.Content.StartsWith("///"))
                    break;
                //Otherwise, skip comments
                continue;
            }
            if (ii > 0 && RequiresWhitespaceSep(t.Type) && RequiresWhitespaceSep(tokens[ii - 1].Type)) {
                throw new Exception("Tokens must be separated:\n" +
                                    t.Position.Start.PrettyPrintLocation(source) +
                                    $"\nThe previous token {tokens[ii - 1]} and this token {t} must be separated by " +
                                    $"whitespace or an operator.");
            }
            paren.Update(in t);
            brace.Update(in t);
            bracket.Update(in t);
            
            if (firstNonWSTokenOnLine) {
                t = t.WithFlags(TokenFlags.PrecededByNewline);
                if (indent == blockIndent)
                    t = t.WithFlags(TokenFlags.ImplicitBreak);
            }
            //set new layout rule indent
            if (processed.Count > 0 && processed[^1].Type == TokenType.OpenBrace) 
                blockIndent = indent;
            if (t.Type == TokenType.CloseBrace)
                blockIndent = indent;
            
            if (ii > 0 && tokens[ii - 1].Type is TokenType.InlineWhitespace or TokenType.Newline)
                t = t.WithFlags(TokenFlags.PrecededByWhitespace);
            if (ii < tokens.Count - 1 && tokens[ii + 1].Type is TokenType.InlineWhitespace or TokenType.Newline)
                t = t.WithFlags(TokenFlags.PostcededByWhitespace);
            if (t.Type == TokenType.Newline) {
                firstNonWSTokenOnLine = true;
                indent = 0;
            } else {
                firstNonWSTokenOnLine = false;
                processed.Add(t);
            }
        }
        paren.AssertClosed();
        brace.AssertClosed();
        bracket.AssertClosed();
        return processed.ToArray();
    }

    private record GroupingHandler(string Source, string Type, string TypePlural, TokenType Open, TokenType Close) {
        private (int opener, int closer)? lastClosedGroupIndex = null;
        private readonly Stack<int> openGroupsIndices = new();
        
        public void Update(in Token t) {
            if (t.Type == Open)
                openGroupsIndices.Push(t.Index);
            else if (t.Type == Close) {
                if (openGroupsIndices.TryPop(out var opener))
                    lastClosedGroupIndex = (opener, t.Index);
                else {
                    var sb = new StringBuilder();
                    sb.Append($"{TypePlural.FirstToUpper()} are not matched correctly:\n");
                    sb.Append(t.Position.Start.PrettyPrintLocation(Source));
                    sb.Append($"\nThis closing {Type} is not matched to an opening {Type}.");
                    if (lastClosedGroupIndex.Try(out var g)) {
                        sb.Append(
                            $"\nThe last successfully parsed {Type} started at {new Position(Source, g.opener)} and " +
                            $"ended at {new Position(Source, g.closer)}.");
                    }
                    throw new Exception(sb.ToString());
                }
            }
        }

        public void AssertClosed() {
            if (openGroupsIndices.TryPop(out var open)) {
                var rest = openGroupsIndices.Reverse().ToArray();
                var pos = new Position(Source, open);
                var sb = new StringBuilder();
                sb.Append($"{TypePlural.FirstToUpper()}s are not matched correctly:\n");
                sb.Append(pos.PrettyPrintLocation(Source));
                sb.Append($"\nThis opening {Type} is not matched to a closing {Type}.");
                if (lastClosedGroupIndex.Try(out var g)) {
                    sb.Append(
                        $"\nThe last successfully parsed {Type} started at {new Position(Source, g.opener)} and " +
                        $"ended at {new Position(Source, g.closer)}.");
                }
                if (rest.Length > 0) {
                    sb.Append(
                        $"\nThere are also other unclosed {TypePlural} at: {string.Join("; ", rest.Select(i => new Position(Source, i)))}");
                }
                throw new Exception(sb.ToString());
            }
        }
    }

    private record TokenWitness(string Source, InputStream<Token> Stream) : ITokenWitness {
        public string SourceStream => Source;
        
        public string ShowError(LocatedParserError error) {
            var pos = Stream.Source.Try(error.Index, out var token) ?
                token.Position :
                new PositionRange(new(Source, Source.Length), new(Source, Source.Length));
            return $"Error at {pos.ToString()}:\n" +
                   pos.Start.PrettyPrintLocation(Source) +
                   $"\n{error.Error.Flatten().Show(Stream)}";
        }

        public ParserError Unexpected(int index) => new ParserError.Unexpected(Stream.Source[index].ToString());

        public Position Step(int step = 1) {
            var i = Stream.Stative.Index;
            if (i + step >= Stream.Source.Length)
                return Stream.Source[^1].Position.End;
            return Stream.Source[i + step].Position.Start;
        }

        public PositionRange ToPosition(int start, int end) =>
            Stream.Source[start].Position.Merge(Stream.Source[end].Position);

        public string ShowConsumed(int start, int end) =>
            new(Source.AsSpan()[Stream.Source[start].Index..Stream.Source[end].Index]);
    }

    public record TokenWitnessCreator(string Source) : ITokenWitnessCreator<Token> {
        public ITokenWitness Create(InputStream<Token> stream) => new TokenWitness(Source, stream);
    }

    public static Parser<Token, Token> TokenOfType(TokenType typ) {
        var err = new ParserError.Expected($"{typ}");
        return input => {
            if (input.Empty || input.Next.Type != typ)
                return new(err, input.Index);
            else
                return new(new(input.Next), null, input.Index, input.Step(1));
        };
    }
    
    public static Parser<Token, Token> TokenOfTypes(params TokenType[] typ) {
        var err = new ParserError.Expected($"one of {string.Join(", ", typ.Select(t => t.ToString()))}");
        return input => {
            if (input.Empty)
                return new(err, input.Index);
            var tokenTyp = input.Next.Type;
            for (int ii = 0; ii < typ.Length; ++ii)
                if (typ[ii] == tokenTyp)
                    return new(new(input.Next), null, input.Index, input.Step(1));
            return new(err, input.Index);
        };
    }
    
    public static Parser<Token, Token> TokenOfValue(string value) {
        var err = new ParserError.Expected(value);
        return input => {
            if (input.Empty || input.Next.Content != value)
                return new(err, input.Index);
            else
                return new(new(input.Next), null, input.Index, input.Step(1));
        };
    }
    
    public static Parser<Token, Token> TokenOfTypeValue(TokenType typ, string value) {
        var err = new ParserError.Expected($"{typ}: {value}");
        return input => {
            if (input.Empty || input.Next.Type != typ || input.Next.Content != value)
                return new(err, input.Index);
            else
                return new(new(input.Next), null, input.Index, input.Step(1));
        };
    }

    private static Token JoinTokens(this Token a, Maybe<Token> b) {
        if (b.Valid) {
            return JoinTokens(a, b.Value);
        } else return a;
    }
    
    private static Token JoinTokens(this Maybe<Token> a, Token b) {
        if (a.Valid) {
            return JoinTokens(a.Value, b);
        } else return b;
    }
    
    private static Token JoinTokens(this Token a, Token b) {
        if (b.Index != a.Position.End.Index)
            throw new Exception("Non-sequential token join");
        return new(a.Type, a.Flags, a.Position.Merge(b.Position), a.Content + b.Content);
    }

    private static Token JoinTokens(this Token a, IEnumerable<Token> bs) {
        foreach (var b in bs)
            a = a.JoinTokens(b);
        return a;
    }

    private static Token JoinTokens(IEnumerable<Token> bs) {
        bool first = true;
        Token token = default;
        foreach (var b in bs) {
            token = first ? b : token.JoinTokens(b);
            first = false;
        }
        if (first)
            throw new Exception("Empty enumerable for JoinTokens");
        return token;
    }

    public static readonly Parser<Token, Token> Ident = TokenOfType(TokenType.Identifier);
    public static readonly Parser<Token, Token> Num = TokenOfType(TokenType.Number);
    public static readonly Parser<Token, Token> ILWhitespace1 = TokenOfType(TokenType.InlineWhitespace);
    public static readonly Parser<Token, Token> Semicolon = TokenOfType(TokenType.Semicolon);


    private static readonly Parser<Token, Token> arrayTypePostfix =
        Sequential(
            TokenOfType(TokenType.OpenBracket),
            TokenOfType(TokenType.CloseBracket),
            (a, b) => a.JoinTokens(b));
    private static readonly Parser<Token, Token> parseTypeIdent =
        //Ident, followed by one of:
        //  ([])+
        //  <(Ident|TypeIdent)+>([])*
        Ident.ThenTry(ChoiceL("Type identifier", 
                Sequential(
                    TokenOfValue("<"),
                    ((Parser<Token,Token>)ParseTypeIdentifier).Or(Ident).SepByAll(
                        Sequential(TokenOfType(TokenType.Comma), ILWhitespace1.Opt(), JoinTokens), 1),
                    TokenOfValue(">"),
                    arrayTypePostfix.Many(),
                    (op, gs, cl, arrs) => 
                        op.JoinTokens(gs).JoinTokens(cl).JoinTokens(arrs)
                ),
                arrayTypePostfix.Many1().FMap(JoinTokens)
            ).Attempt())
            .FMap(x => x.a.JoinTokens(x.b).WithType(TokenType.TypeIdentifier));
        
    private static ParseResult<Token> ParseTypeIdentifier(InputStream<Token> inp) => parseTypeIdent(inp);

    private static readonly Parser<Token, Token> parseV2RV2 =
        Sequential(
            TokenOfValue("<"),
            //attempt on this so in cases like <RX;RY:A> it doesn't parse the A
            Sequential(Num.Opt(), Semicolon, Num.Opt(), TokenOfValue(":"),
                (x, sc, y, c) => x.JoinTokens(sc).JoinTokens(y).JoinTokens(c)).Attempt().Repeat(0, 2),
            Num.Opt(),
            TokenOfValue(">"),
            (op, nrxy, ang, cl) => 
                op.JoinTokens(nrxy).JoinTokens(ang).JoinTokens(cl).WithType(TokenType.V2RV2)
        ).Attempt();

    private static readonly Parser<Token, List<Token>> postprocessor = ChoiceL("token", 
            parseTypeIdent,
            parseV2RV2,
            Any<Token>()
        ).Many();


}
}