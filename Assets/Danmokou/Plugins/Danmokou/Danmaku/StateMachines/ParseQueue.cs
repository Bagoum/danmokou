using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BagoumLib;
using BagoumLib.Reflection;
using Danmokou.Core;
using Danmokou.Reflection;
//using FParsec;
//using FParser;
using JetBrains.Annotations;
using UnityEngine.Profiling;
using Mizuhashi;
using static Danmokou.SM.Parsing.SMParser;

namespace Danmokou.SM.Parsing {
public abstract class IParseQueue {
    public PositionRange Position { get; }
    public abstract Reflector.ReflCtx Ctx { get; }
    public abstract IParseQueue ScanChild();
    public IParseQueue NextChild() => NextChild(out _);
    public abstract IParseQueue NextChild(out int i);
    public virtual bool AllowsScan => true;
    public abstract ParsedUnit? _SoftScan(out int index);
    public abstract ParsedUnit _Scan(out int index);
    public abstract void Advance(out int index);
    public void Advance() => Advance(out _);
    public abstract string Print();
    public abstract string Print(int ii);
    public abstract string PrintCurrent();
    public string WrapThrow(string content) => $"{GetLastPosition()}: {content}\n\t{Print()}";
    public string WrapThrowA(string content, string app) => $"{GetLastPosition()}: {content}\n\t{Print()}{app}";
    public string WrapThrowC(string content) => $"{GetLastPosition()}: {content}\n\t{PrintCurrent()}";
    public string WrapThrow(int ii, string content) => $"{GetLastPosition(ii)}: {content}\n\t{Print(ii)}";
    
    public IParseQueue(PositionRange position) {
        Position = position;
    }

    public void ThrowOnLeftovers(Type t) => ThrowOnLeftovers(() => 
        $"Found extra text when trying to create an object of type {t.SimpRName()}. " +
        $"Make sure your parentheses are grouped correctly and your commas are in place.");
    public virtual void ThrowOnLeftovers(Func<string>? descr = null) { }

    public abstract PositionRange GetLastPosition();
    public abstract PositionRange GetLastPosition(int index);
    public string Scan(out int index) => _Scan(out index).Enforce(index, this);
    public string Scan() => Scan(out _);
    public string? MaybeScan(out int index) => _Scan(out index).TryAsString();
    public string? MaybeScan() => MaybeScan(out _);
    public string Next(out int index) {
        var r = Scan(out index);
        Advance();
        return r;
    }
    public string Next() => Next(out _);

    public virtual bool Empty => _SoftScan(out _) == null;
    public abstract bool IsNewline { get; }
    public bool IsNewlineOrEmpty => Empty || IsNewline;
    protected const string LINE_DELIM = "\n";

    public static readonly HashSet<string> ARR_EMPTY = new() {
        ".", "{}", "_"
    }; 
    public const string ARR_OPEN = "{";
    public const string ARR_CLOSE = "}";

    /// <summary>
    /// Returns the next non-newline word in the stream, but skips the line if it is a property declaration.
    /// </summary>
    /// <returns></returns>
    public abstract string ScanNonProperty();

    public virtual bool AllowPostAggregate => false;
    
    public static IParseQueue Lex(string s) {
        Profiler.BeginSample("State Machine Parser");
        var parsed = SMParser.SMParser2Exec(s).GetOrThrow;
        Profiler.EndSample();
        return new PUListParseQueue(parsed, null); 
    }
}

public class ParenParseQueue : IParseQueue {
    public readonly ParsedUnit.Paren paren;
    public ParsedUnit[][] Items => paren.Item;
    public override Reflector.ReflCtx Ctx { get; }
    private int childIndex;

    public ParenParseQueue(ParsedUnit.Paren p, Reflector.ReflCtx ctx) : base(p.Position) {
        paren = p;
        Ctx = ctx;
    }

    public override IParseQueue ScanChild() => new PUListParseQueue(paren.Item[childIndex], Ctx);
    public override IParseQueue NextChild(out int i) => 
        new PUListParseQueue(Items[i = childIndex++], Ctx);
    public override bool Empty => childIndex >= paren.Item.Length;
    public override bool IsNewline => false;
    public override PositionRange GetLastPosition() => Items.Try(childIndex)?.ToRange() ?? Position;
    public override PositionRange GetLastPosition(int i) => Items[i].ToRange();

    public override bool AllowsScan => false;
    public override ParsedUnit? _SoftScan(out int i) => throw new Exception("Cannot call Scan on a parentheses parser");
    public override ParsedUnit _Scan(out int i) => throw new Exception("Cannot call Scan on a parentheses parser");
    public override void Advance(out int i) => throw new Exception("Cannot call Advance on a parentheses parser");

    public override string Print() => paren.Print();
    public override string Print(int ii) => 
        $"({string.Join(", ", paren.Item.Select((x,i) => (i == ii) ? $"≪{x.Print()}≫" : x.Print()))})";

    public override string PrintCurrent() => Print(childIndex);

    public override string ScanNonProperty() =>
        throw new Exception("Cannot call ScanNonProperty on a parentheses parser");
}

public class PUListParseQueue : IParseQueue {
    public readonly ParsedUnit[] atoms;
    public override Reflector.ReflCtx Ctx { get; }
    public int Index { get; set; }
    
    public PUListParseQueue(ParsedUnit[] atoms, Reflector.ReflCtx? ctx) : 
        base(atoms.ToRange()) {
        this.atoms = atoms;
        Ctx = ctx ?? new Reflector.ReflCtx(this);
    }

    public override IParseQueue ScanChild() {
        if (Index >= atoms.Length) throw new Exception(WrapThrow("This section of text is too short."));
        else
            return atoms[Index] switch {
                ParsedUnit.Paren p => new ParenParseQueue(p, Ctx),
                _ => new NonLocalPUListParseQueue(this)
            };
    }
    public override IParseQueue NextChild(out int i) {
        if (Index >= atoms.Length) throw new Exception(WrapThrow("This section of text is too short."));
        i = Index;
        if (atoms[Index] is ParsedUnit.Paren p) {
            ++Index;
            return new ParenParseQueue(p, Ctx);
        } else return new NonLocalPUListParseQueue(this);
    }

    public override bool IsNewline => Index < atoms.Length && atoms[Index].TryAsString() == LINE_DELIM;

    public override PositionRange GetLastPosition() => GetLastPosition(Index);
    public override PositionRange GetLastPosition(int i) => atoms.Try(i)?.Position ?? Position;
    private void __Scan(out int i) {
        for (i = Index; i < atoms.Length && atoms[i].TryAsString() == LINE_DELIM; ++i) { }
    }
    public override ParsedUnit? _SoftScan(out int i) {
        __Scan(out i);
        return i < atoms.Length ? atoms[Index] : (ParsedUnit?) null;
    }

    public override ParsedUnit _Scan(out int i) {
        __Scan(out i);
        ThrowIfOOB(i);
        return atoms[i];
    }

    public override void Advance(out int i) {
        _Scan(out i);
        Index = i + 1;
    }

    /// <summary>
    /// Returns true if there are leftovers and an error should be thrown.
    /// </summary>
    /// <returns></returns>
    public override void ThrowOnLeftovers(Func<string>? descr = null) {
        //this can get called during the initialization code, which creates a new ReflCtx, so Ctx can be null
        __Scan(out var after_newlines);
        if (after_newlines != atoms.Length) {
            throw new Exception(WrapThrow(after_newlines, descr?.Invoke() ?? "Leftover text found after parsing."));
        }
    }
    
    public override string PrintCurrent() => Print(Index);
    public override string Print() => atoms.Print();
    public override string Print(int ii) {
        var start = ii;
        for (; start > 0 && atoms[start].TryAsString() != LINE_DELIM; --start) {}
        if (atoms[start].TryAsString() == LINE_DELIM) ++start;
        for (; ii < atoms.Length && atoms[ii].TryAsString() == LINE_DELIM; ++ii) { }
        var end = ii;
        for (; end < atoms.Length && atoms[end].TryAsString() != LINE_DELIM; ++end) {}
        StringBuilder sb = new();
        for (int jj = start; jj < end; ++jj) {
            sb.Append((jj == ii) ? $"≪{atoms[jj].Print()}≫" : atoms[jj].Print());
            sb.Append(" ");
        }
        return sb.ToString();
    }

    private void ThrowIfOOB(int i) {
        if (i >= atoms.Length) throw new Exception(this.WrapThrow("The parser ran out of text to read."));
    }

    public override string ScanNonProperty() => ScanNonProperty(null);

    public string ScanNonProperty(int? start) {
        int max = atoms.Length;
        var i = start ?? Index;
        for (; i < max && atoms[i].TryAsString() == LINE_DELIM; ++i) { }
        ThrowIfOOB(i);
        while (true) {
            if (atoms[i].Enforce(i, this) != SMParser.PROP_KW) return atoms[i].Enforce(i, this);
            for (; i < max && atoms[i].TryAsString() != LINE_DELIM; ++i) { }
            ThrowIfOOB(i);
            for (; i < max && atoms[i].TryAsString() == LINE_DELIM; ++i) { }
            ThrowIfOOB(i);
        }
    }

    public override bool AllowPostAggregate => Ctx.AllowPostAggregate;
}

public class NonLocalPUListParseQueue : IParseQueue {
    private readonly PUListParseQueue root;
    public override Reflector.ReflCtx Ctx => root.Ctx;

    private readonly bool allowPostAggregate;
    public override bool AllowPostAggregate => allowPostAggregate && root.AllowPostAggregate;
    

    public NonLocalPUListParseQueue(PUListParseQueue root, bool allowPostAggregate=false) : base(root.Position) {
        this.root = root;
        this.allowPostAggregate = allowPostAggregate;
    }

    public override bool IsNewline => root.IsNewline;
    public override IParseQueue ScanChild() => root.ScanChild();
    public override IParseQueue NextChild(out int i) => root.NextChild(out i);
    public override PositionRange GetLastPosition() => root.GetLastPosition();
    public override PositionRange GetLastPosition(int i) => root.GetLastPosition(i);

    public override ParsedUnit? _SoftScan(out int i) => root._SoftScan(out i);
    public override ParsedUnit _Scan(out int i) => root._Scan(out i);
    public override void Advance(out int i) => root.Advance(out i);
    public override string Print() => root.Print();
    public override string Print(int ii) => root.Print(ii);
    public override string PrintCurrent() => root.PrintCurrent();
    public override string ScanNonProperty() => root.ScanNonProperty();
}

public static class IParseQueueHelpers {

    public static string Enforce(this ParsedUnit ParsedUnit, int index, IParseQueue q) =>
        ParsedUnit switch {
            ParsedUnit.Str s => s.Item,
            _ => throw new Exception(q.WrapThrow(index, "Expected a string unit, but found parentheses instead."))
        };

    public static string? TryAsString(this ParsedUnit ParsedUnit) =>
        ParsedUnit switch {
            ParsedUnit.Str s => s.Item,
            _ => null
        };

    public static string Print(this ParsedUnit[][] ParsedUnits) => $"({string.Join(", ", ParsedUnits.Select(Print))})";
    public static string Print(this ParsedUnit[] ParsedUnits) => string.Join(" ", ParsedUnits.Select(Print));
    public static string Print(this ParsedUnit ParsedUnit) =>
        ParsedUnit switch {
            ParsedUnit.Str s => s.Item,
            ParsedUnit.Paren p => Print(p.Item),
            _ => ""
        };
}


}