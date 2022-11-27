using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KC = UnityEngine.KeyCode;

namespace Danmokou.Core.DInput {
public static class KeyCodeHelpers {
    public static readonly KC[] AlphanumericKeys = {
        KC.A, KC.B, KC.C, KC.D, KC.E, KC.F, KC.G, KC.H, KC.I, KC.J, KC.K, KC.L, KC.M, KC.N,
        KC.O, KC.P, KC.Q, KC.R, KC.S, KC.T, KC.U, KC.V, KC.W, KC.X, KC.Y, KC.Z,
        KC.Alpha0, KC.Alpha1, KC.Alpha2, KC.Alpha3, KC.Alpha4,
        KC.Alpha5, KC.Alpha6, KC.Alpha7, KC.Alpha8, KC.Alpha9
    };
    public static readonly KC[] TextInputKeys;

    static KeyCodeHelpers() {
        TextInputKeys = AlphanumericKeys.Concat(new[] {
            KeyCode.Minus, KeyCode.Underscore,
            KeyCode.Equals, KeyCode.Plus,
            KeyCode.LeftBracket, KeyCode.LeftCurlyBracket,
            KeyCode.RightBracket, KeyCode.RightCurlyBracket,
            KeyCode.Backslash, KeyCode.Pipe,
            KeyCode.Semicolon, KeyCode.Colon,
            KeyCode.Quote, KeyCode.DoubleQuote,
            KeyCode.Comma, KeyCode.Less,
            KeyCode.Period, KeyCode.Greater,
            KeyCode.Slash, KeyCode.Question,
            KeyCode.BackQuote, KeyCode.Tilde,
            KeyCode.Space
        }).ToArray();
    }

    public static bool IsAlphabetic(this KeyCode k) => k is >= KC.A and <= KC.Z;
    public static bool IsNumeric(this KeyCode k) => k is >= KC.Alpha0 and <= KC.Alpha9;
    
    public static KC Capitalize(this KeyCode k) => k switch {
        KeyCode.Alpha1 => KeyCode.Exclaim,
        KeyCode.Alpha2 => KeyCode.At,
        KeyCode.Alpha3 => KeyCode.Hash,
        KeyCode.Alpha4 => KeyCode.Dollar,
        KeyCode.Alpha5 => KeyCode.Percent,
        KeyCode.Alpha6 => KeyCode.Caret,
        KeyCode.Alpha7 => KeyCode.Ampersand,
        KeyCode.Alpha8 => KeyCode.Asterisk,
        KeyCode.Alpha9 => KeyCode.LeftParen,
        KeyCode.Alpha0 => KeyCode.RightParen,
        KeyCode.Minus => KeyCode.Underscore,
        KeyCode.Equals => KeyCode.Plus,
        KeyCode.LeftBracket => KeyCode.LeftCurlyBracket,
        KeyCode.RightBracket => KeyCode.RightCurlyBracket,
        KeyCode.Backslash => KeyCode.Pipe,
        KeyCode.Semicolon => KeyCode.Colon,
        KeyCode.Quote => KeyCode.DoubleQuote,
        KeyCode.Comma => KeyCode.Less,
        KeyCode.Period => KeyCode.Greater,
        KeyCode.Slash => KeyCode.Question,
        KeyCode.BackQuote => KeyCode.Tilde,
        _ => k
    };

    private static readonly Dictionary<KeyCode, char> keyCodeRender = new() {
        { KC.Exclaim, '!' },
        { KC.At, '@' },
        { KC.Hash, '#' },
        { KC.Dollar, '$' },
        { KC.Percent, '%' },
        { KC.Caret, '^' },
        { KC.Ampersand, '&' },
        { KC.Asterisk, '*' },
        { KC.LeftParen, '(' },
        { KC.RightParen, ')' },
        { KC.Minus, '-' },
        { KC.Underscore, '_' },
        { KC.Equals, '=' },
        { KC.Plus, '+' },
        { KC.LeftBracket, '[' },
        { KC.LeftCurlyBracket, '{' },
        { KC.RightBracket, ']' },
        { KC.RightCurlyBracket, '}' },
        { KC.Backslash, '\\' },
        { KC.Pipe, '|' },
        { KC.Semicolon, ';' },
        { KC.Colon, ':' },
        { KC.Quote, '\'' },
        { KC.DoubleQuote, '"' },
        { KC.Comma, ',' },
        { KC.Less, '<' },
        { KC.Period, '.' },
        { KC.Greater, '>'},
        { KC.Slash, '/'  },
        { KC.Question, '?' },
        { KC.BackQuote, '`' },
        { KC.Tilde, '~' },
        { KC.Space, ' ' },
        { KC.LeftArrow, '←' },
        { KC.RightArrow, '→' },
        { KC.UpArrow, '↑' },
        { KC.DownArrow, '↓' },
    };

    /// <summary>
    /// Render the key code in lowercase text as it would be used for text input.
    /// Control keys like F*, shift, ctrl, etc return null.
    /// <example>Render(KC.Question) => '?'
    /// <br/>Render(KC.B) => 'b'
    /// <br/>Render(KC.Space) => ' '</example>
    /// </summary>
    public static char? RenderAsText(this KeyCode k) {
        if (k.IsAlphabetic())
            return (char)('a' + (k - KC.A));
        if (k.IsNumeric())
            return (char)('0' + (k - KC.Alpha0));
        return keyCodeRender.TryGetValue(k, out var v) ? v : null;
    }


    /// <summary>
    /// Render the key code in uppercase text, or provide a description of the key.
    /// Does not work for joystick keycodes.
    /// <example>Render(KC.Question) => '?'
    /// <br/>Render(KC.B) => 'b'
    /// <br/>Render(KC.Space) => 'Space'
    /// <br/>Render(KC.LeftShift) => 'LeftShift'</example>
    /// </summary>
    public static string RenderInformative(this KeyCode k) => k switch {
        _ when k.IsAlphabetic() => ((char)('A' + (k - KC.A))).MakeString(),
        _ when k.IsNumeric() => ((char)('0' + (k - KC.Alpha0))).MakeString(),
        KC.Space => "Space",
        _ => keyCodeRender.TryGetValue(k, out var v) ? v.MakeString() : k.KeyCodeToString()
    };

    private static readonly Dictionary<KeyCode, string> keycodeCache = new(16);
    private static string KeyCodeToString(this KeyCode k) {
        if (!keycodeCache.TryGetValue(k, out var s))
            keycodeCache[k] = s = k.ToString();
        return s;
    }
}
}