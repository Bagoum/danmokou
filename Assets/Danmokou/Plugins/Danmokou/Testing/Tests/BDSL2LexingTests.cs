using System;
using System.Collections.Generic;
using System.Linq;
using Danmokou.Core;
using Danmokou.Danmaku.Patterns;
using Danmokou.Services;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Expressions;
using Danmokou.GameInstance;
using Danmokou.Reflection;
using Danmokou.Reflection2;
using Danmokou.SM;
using Danmokou.SM.Parsing;
using Mizuhashi;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using static NUnit.Framework.Assert;
using static Danmokou.Testing.TAssert;
using static Danmokou.Reflection2.Lexer;

namespace Danmokou.Testing {

public static class BDSL2LexingTests {
    [Test]
    public static void TestLexingFailures() {
        ThrowsMessage(@"The previous token ""15.42"" (Number) and this token ""world"" (Identifier) must be separated by whitespace", () => Lexer.Lex("15.42world"));
        ThrowsMessage("This closing parenthesis is not matched to an opening parenthesis.\nThe last successfully parsed parenthesis started at Line 1, Col 3 and ended at Line 1, Col 9", () => Lexer.Lex("fn(\")))\"), )"));
        ThrowsMessage("This opening parenthesis is not matched to a closing parenthesis." +
                      "\nThe last successfully parsed parenthesis started at Line 1, Col 14 and ended at Line 1, Col 15." +
                      "\nThere are also other unclosed parentheses at: Line 1, Col 2; Line 1, Col 3", () => 
            Lexer.Lex("m((y<type>(fn()"));
        ThrowsMessage("The previous token \"List\" (Identifier) and this token \"<>\" (V2RV2) must be separated by whitespace or an operator", () => Lexer.Lex("List<>"));
    }

    private const TokenType id = TokenType.Identifier;
    private const TokenType op = TokenType.Operator;

    [Test]
    public static void TestTypeIdents() {
        var ts = MakeFromSequence((id, "List"), (op, "<"), (null, " "), (op, ">"));
        ts[1] = ts[1].WithFlags(TokenFlags.PostcededByWhitespace);
        ts[2] = ts[2].WithFlags(TokenFlags.PrecededByWhitespace);
        ListEq(Lexer.Lex("List< >"), ts);
        ListEq(Lexer.Lex("List<List<int, Node<float>>>"), MakeFromSequence((TokenType.TypeIdentifier, "List<List<int, Node<float>>>")));
        ListEq(Lexer.Lex("List<List<int, Node<float>>"), MakeFromSequence(
            (id, "List"),
            (op, "<"),
            (TokenType.TypeIdentifier, "List<int, Node<float>>")
        ));
        ListEq(Lexer.Lex("List<int>[]"), MakeFromSequence((TokenType.TypeIdentifier, "List<int>[]")));
        ListEq(Lexer.Lex("List<int>[][]"), MakeFromSequence((TokenType.TypeIdentifier, "List<int>[][]")));
        ListEq(Lexer.Lex("List<int[][]>[]"), MakeFromSequence((TokenType.TypeIdentifier, "List<int[][]>[]")));
        ListEq(Lexer.Lex("int[][]"), MakeFromSequence((TokenType.TypeIdentifier, "int[][]")));
        ListEq(Lexer.Lex("int"), MakeFromSequence((TokenType.Identifier, "int")));
    }
    
    
    [Test]
    public static void TestV2RV2() {
        ListEq(Lexer.Lex("<12;24:;:>"), MakeFromSequence((TokenType.V2RV2, "<12;24:;:>")));
        var w = "<;23:4>";
        ListEq(Lexer.Lex(ref w, out var s), MakeFromSequence((TokenType.V2RV2, "<;23:4>")));
        Debug.Log(string.Join("\n", s.Rollbacks.Select(r => r.Show(s)).ToArray()));
        ListEq(Lexer.Lex("<40h>"), MakeFromSequence((TokenType.V2RV2, "<40h>")));
        ListEq(Lexer.Lex("<>"), MakeFromSequence((TokenType.V2RV2, "<>")));
    }
    
    
    private static List<Token> MakeFromSequence(params (TokenType? type, string token)[] fragments) {
        var result = new List<Token>();
        var pos = new Position("", 0);
        foreach (var (type, token) in fragments) {
            if (type is { } t)
                result.Add(new(t, pos, token));
            pos = pos.Step(token, token.Length);
        }
        return result;
    }
}
}