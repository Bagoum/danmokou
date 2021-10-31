using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BagoumLib;
using Danmokou.Core;
using Danmokou.Reflection;
//using FParsec;
//using FParser;
using JetBrains.Annotations;
using UnityEngine.Profiling;
//using LPU = System.ValueTuple<FParser.SMParser.ParsedUnit, FParsec.Position>;
using Mizuhashi;
using LPU = System.ValueTuple<Danmokou.SM.Parsing.SMParser.ParsedUnit, Mizuhashi.Position>;

namespace Danmokou.SM.Parsing {
public abstract class IParseQueue {
    public Position Position { get; }
    public abstract Reflector.ReflCtx Ctx { get; }
    public abstract IParseQueue ScanChild();
    public IParseQueue NextChild() => NextChild(out _);
    public abstract IParseQueue NextChild(out int i);
    public virtual bool AllowsScan => true;
    public abstract LPU? _SoftScan(out int index);
    public abstract LPU _Scan(out int index);
    public abstract void Advance(out int index);
    public void Advance() => Advance(out _);
    public abstract string Print();
    public abstract string Print(int ii);
    public abstract string PrintCurrent();
    public string WrapThrow(string content) => $"Line {GetLastLine()}: {content}\n\t{Print()}";
    public string WrapThrowA(string content, string app) => $"Line {GetLastLine()}: {content}\n\t{Print()}{app}";
    public string WrapThrowC(string content) => $"Line {GetLastLine()}: {content}\n\t{PrintCurrent()}";
    public string WrapThrow(int ii, string content) => $"Line {GetLastLine(ii)}: {content}\n\t{Print(ii)}";
    
    public IParseQueue(Position pos) {
        Position = pos;
    }

    public void ThrowOnLeftovers(Type t) => ThrowOnLeftovers(() => 
        $"Found extra text when trying to create an object of type {t.RName()}. " +
        $"Make sure your parentheses are grouped correctly and your commas are in place.");
    public virtual void ThrowOnLeftovers(Func<string>? descr = null) { }

    public abstract Position GetLastPosition();
    public abstract Position GetLastPosition(int index);
    public int GetLastLine() => GetLastPosition().Line;
    public int GetLastLine(int index) => GetLastPosition(index).Line;
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

    public static readonly HashSet<string> ARR_EMPTY = new HashSet<string>() {
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
        return new PUListParseQueue(parsed, parsed[0].Item2, null); 
    }
}

public class ParenParseQueue : IParseQueue {
    public readonly LPU[][] paren;
    public override Reflector.ReflCtx Ctx { get; }
    private int childIndex;

    public ParenParseQueue(LPU[][] p, Position pos, Reflector.ReflCtx ctx) : base(pos) {
        paren = p;
        Ctx = ctx;
    }

    public override IParseQueue ScanChild() => new PUListParseQueue(paren[childIndex], Position, Ctx);
    public override IParseQueue NextChild(out int i) => 
        new PUListParseQueue(paren[i = childIndex++], Position, Ctx);
    public override bool Empty => childIndex >= paren.Length;
    public override bool IsNewline => false;
    public override Position GetLastPosition() => paren.Try(childIndex)?.TryN(0)?.Item2 ?? Position;
    public override Position GetLastPosition(int i) => paren[i][0].Item2;

    public override bool AllowsScan => false;
    public override LPU? _SoftScan(out int i) => throw new Exception("Cannot call Scan on a parentheses parser");
    public override LPU _Scan(out int i) => throw new Exception("Cannot call Scan on a parentheses parser");
    public override void Advance(out int i) => throw new Exception("Cannot call Advance on a parentheses parser");

    public override string Print() => paren.Print();
    public override string Print(int ii) => 
        $"({string.Join(", ", paren.Select((x,i) => (i == ii) ? $"≪{x.Print()}≫" : x.Print()))})";

    public override string PrintCurrent() => Print(childIndex);

    public override string ScanNonProperty() =>
        throw new Exception("Cannot call ScanNonProperty on a parentheses parser");
}

public class PUListParseQueue : IParseQueue {
    public readonly (SMParser.ParsedUnit, Position)[] atoms;
    public override Reflector.ReflCtx Ctx { get; }
    public int Index { get; set; }
    
    public PUListParseQueue((SMParser.ParsedUnit, Position)[] atoms, Position pos, Reflector.ReflCtx? ctx) : base(pos) {
        this.atoms = atoms;
        Ctx = ctx ?? new Reflector.ReflCtx(this);
    }

    public override IParseQueue ScanChild() {
        if (Index >= atoms.Length) throw new Exception(WrapThrow("This section of text is too short."));
        else
            return atoms[Index].Item1 switch {
                SMParser.ParsedUnit.Paren p => new ParenParseQueue(p.Item, atoms[Index].Item2, Ctx),
                _ => new NonLocalPUListParseQueue(this)
            };
    }
    public override IParseQueue NextChild(out int i) {
        if (Index >= atoms.Length) throw new Exception(WrapThrow("This section of text is too short."));
        i = Index;
        return atoms[Index].Item1 switch {
            SMParser.ParsedUnit.Paren p => new ParenParseQueue(p.Item, atoms[Index++].Item2, Ctx),
            _ => new NonLocalPUListParseQueue(this)
        };
    }

    public override bool IsNewline => Index < atoms.Length && atoms[Index].TryAsString() == LINE_DELIM;

    public override Position GetLastPosition() => GetLastPosition(Index);
    public override Position GetLastPosition(int i) => atoms.TryN(i)?.Item2 ?? Position;
    private void __Scan(out int i) {
        for (i = Index; i < atoms.Length && atoms[i].TryAsString() == LINE_DELIM; ++i) { }
    }
    public override LPU? _SoftScan(out int i) {
        __Scan(out i);
        return i < atoms.Length ? atoms[Index] : (LPU?) null;
    }

    public override LPU _Scan(out int i) {
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
        StringBuilder sb = new StringBuilder();
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
    public override Position GetLastPosition() => root.GetLastPosition();
    public override Position GetLastPosition(int i) => root.GetLastPosition(i);

    public override LPU? _SoftScan(out int i) => root._SoftScan(out i);
    public override LPU _Scan(out int i) => root._Scan(out i);
    public override void Advance(out int i) => root.Advance(out i);
    public override string Print() => root.Print();
    public override string Print(int ii) => root.Print(ii);
    public override string PrintCurrent() => root.PrintCurrent();
    public override string ScanNonProperty() => root.ScanNonProperty();
}

public static class IParseQueueHelpers {

    public static string Enforce(this LPU lpu, int index, IParseQueue q) =>
        lpu.Item1 switch {
            SMParser.ParsedUnit.Str s => s.Item,
            _ => throw new Exception(q.WrapThrow(index, "Expected a string unit, but found parentheses instead."))
        };

    public static string? TryAsString(this LPU lpu) =>
        lpu.Item1 switch {
            SMParser.ParsedUnit.Str s => s.Item,
            _ => null
        };

    public static string Print(this LPU[][] lpus) => $"({string.Join(", ", lpus.Select(Print))})";
    public static string Print(this LPU[] lpus) => string.Join(" ", lpus.Select(Print));
    public static string Print(this LPU lpu) =>
        lpu.Item1 switch {
            SMParser.ParsedUnit.Str s => s.Item,
            SMParser.ParsedUnit.Paren p => Print(p.Item),
            _ => ""
        };
}


}