using LanguageExt;
using static LanguageExt.Prelude;
using static LanguageExt.Parsec.Prim;
using static LanguageExt.Parsec.Char;
using static ParserCS.Common;
using System.Collections.Generic;
using System;
using System.Collections;
using System.Linq;
using LanguageExt.Parsec;
using System.Collections.Immutable;
using System.Text;
using BagoumLib;
using BagoumLib.Functional;

namespace ParserCS {
public static class AAParser {

    public const char INVOKE = '$';
    public const char TAG_OPEN = '<';
    public const char TAG_CLOSE = '>';

    private static readonly Dictionary<char, Punct> PunctMap = new Dictionary<char, Punct>() {
        {',', Punct.Comma},
        {'.', Punct.Period},
        {'!', Punct.Period},
        {'?', Punct.Period},
        {';', Punct.Semicolon},
        {':', Punct.Semicolon},
    };

    private static bool IsPunctuation(char c) => 
        PunctMap.ContainsKey(c);
    public static Punct ResolvePunctuation(char c, Punct deflt) => 
        PunctMap.TryGetValue(c, out var p) ? p : deflt;

    private static bool InTag(char c) =>
        c != TAG_OPEN && c != TAG_CLOSE;

    private static bool NormalLetter(char c) =>
        c != INVOKE && !IsPunctuation(c) && !char.IsWhiteSpace(c) && InTag(c);

    public enum Punct {
        Space,
        Comma,
        Period,
        Semicolon,
        Ellipsis
    }

    public static T Switch<T>(this Punct p, T space, T comma, T period, T semicolon, T ellipsis) =>
        p switch {
            Punct.Space => space,
            Punct.Comma => comma,
            Punct.Period => period,
            Punct.Semicolon => semicolon,
            Punct.Ellipsis => ellipsis,
            _ => space
        };


    public class TextUnit<T> {
        public enum Type {
            Tag, // string
            Atom, // string
            Newline,
            Punct, // string, punct
            Ref // T
        }

        public readonly Type type;
        public readonly string stringVal;
        public readonly Punct punctVal;
        public readonly T refObj;

        public TextUnit(Type type, string stringVal="", Punct punctVal=default, T refObj=default) {
            this.type = type;
            this.stringVal = stringVal;
            this.punctVal = punctVal;
            this.refObj = refObj;
        }

        public static TextUnit<T> Tag(string content) => new TextUnit<T>(Type.Tag, content);
        public static TextUnit<T> Atom(string content) => new TextUnit<T>(Type.Atom, content);
        public static TextUnit<T> Punct(string content, Punct p) => new TextUnit<T>(Type.Punct, content, p);
        public static TextUnit<T> Ref(T obj) => new TextUnit<T>(Type.Ref, refObj: obj);
        public static readonly TextUnit<T> Newline = new TextUnit<T>(Type.Newline);
    }

    public class TextCommand {
        public enum Type {
            TextWait, // string, float
            Space, // string
            Newline,
            OpenTag, // string
            CloseTag,
            Wait, // float
            Ref // T
        }
    }
    public class TextCommand<T> : TextCommand {

        public readonly Type type;
        public readonly string stringVal;
        public readonly float wait;
        public readonly T refObj;

        public TextCommand(Type type, string stringVal="", float wait=0, T refObj=default) {
            this.type = type;
            this.stringVal = stringVal;
            this.wait = wait;
            this.refObj = refObj;
        }

        public static TextCommand<T> Text(string content, float wait = 0) =>
            new TextCommand<T>(Type.TextWait, content, wait);
        public static TextCommand<T> Space(string content) => new TextCommand<T>(Type.Space, content);
        public static TextCommand<T> OpenTag(string content) => new TextCommand<T>(Type.OpenTag, content);
        public static TextCommand<T> Wait(float wait) => new TextCommand<T>(Type.Wait, wait: wait);
        public static TextCommand<T> Ref(T obj) => new TextCommand<T>(Type.Ref, refObj: obj);
        public static readonly TextCommand<T> Newline = new TextCommand<T>(Type.Newline);
        public static readonly TextCommand<T> CloseTag = new TextCommand<T>(Type.CloseTag);


        public void Resolve(Action<string, float> text, Action<string> space, Action newline,
            Action<string> openTag, Action closeTag, Action<float> wait_, Action<T> ref_) {
            if      (type == Type.TextWait)
                text(stringVal, wait);
            else if (type == Type.Space)
                space(stringVal);
            else if (type == Type.Newline)
                newline();
            else if (type == Type.OpenTag)
                openTag(stringVal);
            else if (type == Type.CloseTag)
                closeTag();
            else if (type == Type.Wait)
                wait_(wait);
            else if (type == Type.Ref)
                ref_(refObj);
        }
        
    }

    public readonly struct ExportAcc<T> {
        public readonly TextCommand<T>[][] commands;
        public ExportAcc(TextCommand<T>[][] commands) {
            this.commands = commands;
        }
    }

    public class Config<T> {
        public float speed;
        public int charsPerBlock;
        public float blockOps;
        //Punct units are mapped to longer pauses as well as events (such as SFX).
        public Func<Punct, float> punctOps;
        public Maybe<T> blockEvent;
        public Func<Punct, Maybe<T>> punctEvent;

        public float BlockWait => blockOps / speed;

        public static Config<T> Default => new Config<T>() {
            speed = 10,
            charsPerBlock = 3,
            blockOps = 1,
            punctOps = p => p.Switch(2, 4, 5, 6, 8),
            blockEvent = Maybe<T>.None,
            punctEvent = _ => Maybe<T>.None
        };
    }
    
    

    private static readonly Parser<string> NormalWord = 
        many1String(satisfy(NormalLetter));

    private static readonly Parser<string> InvokeWord = 
        many1String(satisfy(c => char.IsLetterOrDigit(c) || c == '_' || c == '.'));

    private static Parser<List<IEnumerable<TextUnit<T>>>> _CreateParser<T>(Func<string, string, Errorable<T>> invoker) {
        static IEnumerable<TextUnit<T>> L(TextUnit<T> t) => new List<TextUnit<T>>() {t};
        return many1(choice(
            NormalWord.Map(s => L(TextUnit<T>.Atom(s))),
            BetweenChars(TAG_OPEN, TAG_CLOSE, manyString(satisfy(InTag)))
                .Map(s => L(TextUnit<T>.Tag($"{TAG_OPEN}{s}{TAG_CLOSE}"))),
            Sequential(ch(INVOKE), InvokeWord, Paren1(InvokeWord), (_, key, arg) => invoker(key, arg))
                .SelectMany(errb => errb.Valid ?
                    result(L(TextUnit<T>.Ref(errb.Value))) :
                    failure<IEnumerable<TextUnit<T>>>(errb.JoinedErrors)),
            many1String(satisfy(WhiteInline)).Map(x => L(TextUnit<T>.Punct(x, Punct.Space))),
            endOfLine.Map(_ => L(TextUnit<T>.Newline)), //No comments within text.
            many1(satisfy(IsPunctuation)).Map(ps => 
                ps.MaxConsecutive('.') > 1 ? 
                    ps.Select(x => TextUnit<T>.Punct(new string(x, 1), Punct.Ellipsis)) : 
                    ps.Select(x => TextUnit<T>.Punct(new string(x, 1), ResolvePunctuation(x, Punct.Comma))))
        ));
    }

    public static Func<string, Errorable<IEnumerable<TextUnit<T>>>>
        CreateParser<T>(Func<string, string, Errorable<T>> invoker) {
        var parser = _CreateParser(invoker);
        return s => {
            var res = parse(parser, s);
            return res.IsFaulted ?
                Errorable<IEnumerable<TextUnit<T>>>.Fail(res.Reply.Error.ToString()) :
                Errorable<IEnumerable<TextUnit<T>>>.OK(res.Reply.Result.Join());
        };
    }

    public static Func<Config<T>, string, Errorable<TextCommand<T>[]>>
        CreateCommandParser<T>(Func<string, string, Errorable<T>> invoker) {
        var parser = CreateParser(invoker);
        return (cfg, s) => parser(s).Map(res => ExportToCommands(cfg, res).ToArray());
    }

    public static IEnumerable<TextCommand<T>> ExportToCommands<T>(Config<T> cfg, IEnumerable<TextUnit<T>> parsed) {
        var sb = new StringBuilder();
        foreach (var unit in parsed) {
            var s = unit.stringVal;
            if         (unit.type == TextUnit<T>.Type.Tag) {
                yield return TextCommand<T>.Text(s);
            }  else if (unit.type == TextUnit<T>.Type.Atom) {
                int hang = 0;
                for (int ii = 0; ii < s.Length; ++ii) {
                    sb.Append(s[ii]);
                    if (++hang == cfg.charsPerBlock) {
                        yield return TextCommand<T>.Text(sb.ToString(), cfg.BlockWait);
                        hang = 0;
                        sb.Clear();
                    }
                    if (hang == 1 && cfg.blockEvent.Try(out var t))
                        yield return TextCommand<T>.Ref(t);
                }
                if (hang > 0)
                    yield return TextCommand<T>.Text(sb.ToString(), hang / (float)cfg.charsPerBlock * cfg.BlockWait);
                sb.Clear();
            } else if (unit.type == TextUnit<T>.Type.Newline) {
                yield return TextCommand<T>.Newline;
            } else if (unit.type == TextUnit<T>.Type.Punct) {
                var ops = cfg.punctOps(unit.punctVal);
                yield return string.IsNullOrWhiteSpace(s) ?
                    TextCommand<T>.Space(s) :
                    TextCommand<T>.Text(s);
                if (cfg.punctEvent(unit.punctVal).Try(out var ev)) 
                    yield return TextCommand<T>.Ref(ev);
                if (ops > 0) 
                    yield return TextCommand<T>.Wait(ops / cfg.speed);
            } else if (unit.type == TextUnit<T>.Type.Ref) {
                yield return TextCommand<T>.Ref(unit.refObj);
            }
        }
    }
}
}