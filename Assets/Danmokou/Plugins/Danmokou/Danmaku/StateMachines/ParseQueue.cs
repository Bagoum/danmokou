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
    /// <summary>
    /// Returns the position range spanned by this entire parse queue.
    /// This is not entirely accurate when the queue is not <see cref="ParenParseQueue"/>, but is close enough for debug/logging purposes.
    /// </summary>
    public abstract PositionRange Position { get; }
    /// <summary>
    /// Returns the position range spanned by the parse unit at the given index within this parse queue.
    /// This is accurate.
    /// </summary>
    public abstract PositionRange PositionOfObject(int index);
    public abstract Reflector.ReflCtx Ctx { get; }
    public string AsFileLink(Reflector.MethodSignature sig) => Ctx.AsFileLink(sig);
    public abstract IParseQueue NextChild();
    /// <summary>
    /// Get the parse unit at the current index.
    /// <br/>Returns null if the queue is empty.
    /// </summary>
    public abstract ParsedUnit? MaybeGetCurrentUnit(out int index);

    /// <summary>
    /// Get the parse unit at the current index.
    /// <br/>Throws if the queue is empty.
    /// </summary>
    public ParsedUnit GetCurrentUnit(out int index) {
        if (MaybeGetCurrentUnit(out index) is { } p)
            return p;
        throw OOBException();
    }
    
    public abstract void Advance();
    public abstract string Print();
    public abstract string PrintHighlight(int ii);
    
    public ReflectionException OOBException() => this.WrapThrow("The parser ran out of text to read.");
    public ReflectionException WrapThrow(string content, Exception? inner = null) => 
        new ReflectionException(Position, $"{content}\n\t{Print()}", inner);
    public ReflectionException WrapThrowAppend(string content, string app, Exception? inner = null) => 
        new ReflectionException(Position, $"{content}\n\t{Print()}{app}", inner);
    
    /// <summary>
    /// Format an exception that shows the contents of this queue, highlighting the object at the given index.
    /// </summary>
    public ReflectionException WrapThrowHighlight(int index, string content, Exception? inner = null) => 
        new ReflectionException(Position, $"{content}\n\t{PrintHighlight(index)}", inner) {
            HighlightedPosition = PositionOfObject(index)
        };

    public void ThrowOnLeftovers(Type t) => ThrowOnLeftovers(() => 
        $"Successfully created an object of type {t.SimpRName()}, but then found extra text (in ≪≫). This may be because you have forgotten to put a comma before the highlighted text.");
    public virtual void ThrowOnLeftovers(Func<string>? descr = null) { }
    public string Scan() => ScanUnit(out _).Item;
    /// <summary>
    /// Get the parse unit at the current index.
    /// <br/>Throws if the queue is empty or the parse unit is not a string.
    /// </summary>
    public ParsedUnit.Str ScanUnit(out int index) => GetCurrentUnit(out index).Enforce(index, this);
    public string? MaybeScan() => GetCurrentUnit(out _).TryAsString();
    public string Next() => NextUnit(out _).Item;
    /// <summary>
    /// Get the parse unit at the current index and advance the index.
    /// <br/>Throws if the queue is empty or the parse unit is not a string.
    /// </summary>
    public ParsedUnit.Str NextUnit(out int index) {
        var r = ScanUnit(out index);
        Advance();
        return r;
    }

    public virtual bool Empty => MaybeGetCurrentUnit(out _) == null;
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
        var parsed = SMParser.ExportSMParserToParsedUnits(s);
        Profiler.EndSample();
        if (parsed.IsLeft)
            return new PUListParseQueue((parsed.Left, parsed.Left.ToRange()), null);
        throw new Exception(string.Join("\n", parsed.Right.Select(p => p.Show(s))));
    }
}

public class ParenParseQueue : IParseQueue {
    public override PositionRange Position => paren.Position;
    public readonly ParsedUnit.Paren paren;
    public (ParsedUnit[] units, PositionRange position)[] Items => paren.Items;
    public override Reflector.ReflCtx Ctx { get; }
    private int childIndex;

    public ParenParseQueue(ParsedUnit.Paren p, Reflector.ReflCtx ctx) {
        paren = p;
        Ctx = ctx;
    }

    public override PositionRange PositionOfObject(int index) => Items[index].position;
    public override IParseQueue NextChild() => 
        new PUListParseQueue(Items[childIndex++], Ctx);
    public override bool Empty => childIndex >= paren.Items.Length;
    public override bool IsNewline => false;

    public override ParsedUnit? MaybeGetCurrentUnit(out int i) => 
        throw new Exception($"Cannot call {nameof(MaybeGetCurrentUnit)} on a parentheses parser");
    public override void Advance() => throw new Exception("Cannot call Advance on a parentheses parser");

    public override string Print() => paren.Print();
    public override string PrintHighlight(int ii) => 
        $"({string.Join(", ", paren.Items.Select((x,i) => (i == ii) ? $"≪{x.units.Print()}≫" : x.units.Print()))})";

    public override string ScanNonProperty() =>
        throw new Exception("Cannot call ScanNonProperty on a parentheses parser");
}

public class PUListParseQueue : IParseQueue {
    public override PositionRange Position { get; }
    public readonly ParsedUnit[] atoms;
    public override Reflector.ReflCtx Ctx { get; }
    public int Index { get; set; }
    
    public PUListParseQueue((ParsedUnit[] atoms, PositionRange pos) item, Reflector.ReflCtx? ctx) {
        this.Position = item.pos;
        this.atoms = item.atoms;
        Ctx = ctx ?? new Reflector.ReflCtx(this);
    }
    public override PositionRange PositionOfObject(int index) => atoms[index].Position;
    public override IParseQueue NextChild() {
        if (Index >= atoms.Length) throw WrapThrow("This section of text is too short.");
        if (atoms[Index] is ParsedUnit.Paren p) {
            ++Index;
            return new ParenParseQueue(p, Ctx);
        } else {
            //Skip past newlines so it's not captured in the NL location range
            for (; Index < atoms.Length && atoms[Index].TryAsString() == LINE_DELIM; ++Index) { }
            return new NonLocalPUListParseQueue(this);
        }
    }

    public override bool IsNewline => Index < atoms.Length && atoms[Index].TryAsString() == LINE_DELIM;

    public PositionRange PositionOfCurrentOrLastObject() => PositionOfObject(Math.Min(atoms.Length - 1, Index));
    
    public override ParsedUnit? MaybeGetCurrentUnit(out int i) {
        for (i = Index; i < atoms.Length && atoms[i].TryAsString() == LINE_DELIM; ++i) { }
        return i < atoms.Length ? atoms[i] : (ParsedUnit?) null;
    }

    public override void Advance() {
        GetCurrentUnit(out var i);
        Index = i + 1;
    }

    /// <summary>
    /// Returns true if there are leftovers and an error should be thrown.
    /// </summary>
    /// <returns></returns>
    public override void ThrowOnLeftovers(Func<string>? descr = null) {
        //this can get called during the initialization code, which creates a new ReflCtx, so Ctx can be null
        MaybeGetCurrentUnit(out var after_newlines);
        if (after_newlines != atoms.Length) {
            throw WrapThrowHighlight(after_newlines, descr?.Invoke() ?? "Leftover text found after parsing.");
        }
    }
    
    public override string Print() => atoms.Print();
    public override string PrintHighlight(int ii) {
        var start = ii;
        for (; start > 0 && atoms[start].TryAsString() != LINE_DELIM; --start) {}
        //if (atoms[start].TryAsString() == LINE_DELIM) --start;
        //Also show the previous line
        //for (; start > 0 && atoms[start].TryAsString() != LINE_DELIM; --start) {}
        if (atoms[start].TryAsString() == LINE_DELIM) ++start;
        
        var end = ii;
        for (; end < atoms.Length && atoms[end].TryAsString() != LINE_DELIM; ++end) {}
        //Also show the next line
        //if (atoms.Try(end)?.TryAsString() == LINE_DELIM) ++end;
        //for (; end < atoms.Length && atoms[end].TryAsString() != LINE_DELIM; ++end) {}
        StringBuilder sb = new();
        for (int jj = start; jj < end; ++jj) {
            sb.Append((jj == ii) ? $"≪{atoms[jj].Print()}≫" : atoms[jj].Print());
            sb.Append(" ");
        }
        return sb.ToString();
    }

    private void ThrowIfOOB(int i) {
        if (i >= atoms.Length) throw OOBException();
    }

    public override string ScanNonProperty() => ScanNonProperty(null);

    public string ScanNonProperty(int? start) {
        int max = atoms.Length;
        var i = start ?? Index;
        for (; i < max && atoms[i].TryAsString() == LINE_DELIM; ++i) { }
        ThrowIfOOB(i);
        while (true) {
            if (atoms[i].Enforce(i, this).Item != SMParser.PROP_KW) 
                return atoms[i].Enforce(i, this).Item;
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
    private readonly Position startPosition;
    public override PositionRange Position => new(startPosition, root.PositionOfCurrentOrLastObject().Start);
    public override Reflector.ReflCtx Ctx => root.Ctx;

    private readonly bool allowPostAggregate;
    public override bool AllowPostAggregate => allowPostAggregate && root.AllowPostAggregate;
    

    public NonLocalPUListParseQueue(PUListParseQueue root, bool allowPostAggregate=false) {
        this.root = root;
        this.startPosition = root.PositionOfCurrentOrLastObject().Start;
        this.allowPostAggregate = allowPostAggregate;
    }

    public override bool IsNewline => root.IsNewline;
    public override PositionRange PositionOfObject(int index) => root.PositionOfObject(index);
    public override IParseQueue NextChild() => root.NextChild();
    public override ParsedUnit? MaybeGetCurrentUnit(out int i) => root.MaybeGetCurrentUnit(out i);
    public override void Advance() => root.Advance();
    public override string Print() => root.Print();
    public override string PrintHighlight(int ii) => root.PrintHighlight(ii);
    public override string ScanNonProperty() => root.ScanNonProperty();
}

public static class IParseQueueHelpers {

    public static ParsedUnit.Str Enforce(this ParsedUnit ParsedUnit, int index, IParseQueue q) =>
        ParsedUnit switch {
            ParsedUnit.Str s => s,
            _ => throw q.WrapThrowHighlight(index, "Expected a string unit, but found parentheses instead.")
        };

    public static string? TryAsString(this ParsedUnit ParsedUnit) =>
        ParsedUnit switch {
            ParsedUnit.Str s => s.Item,
            _ => null
        };

    public static string Print(this (ParsedUnit[], PositionRange)[] ParsedUnits) => 
        $"({string.Join(", ", ParsedUnits.Select(p => Print(p.Item1)))})";
    public static string Print(this ParsedUnit[] ParsedUnits) => string.Join(" ", ParsedUnits.Select(Print));
    public static string Print(this ParsedUnit ParsedUnit) =>
        ParsedUnit switch {
            ParsedUnit.Str s => s.Item,
            ParsedUnit.Paren p => Print(p.Items),
            _ => ""
        };
}


}