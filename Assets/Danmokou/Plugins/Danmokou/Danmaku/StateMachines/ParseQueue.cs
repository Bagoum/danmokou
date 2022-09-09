using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using BagoumLib;
using BagoumLib.Functional;
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
    /// Returns the position range spanned from the start of this parse queue up to but not including the object at the given index.
    /// This is accurate, but due to whitespace, its end may not align with <see cref="PositionOfObject"/>'s start.
    /// </summary>
    public abstract PositionRange PositionUpToObject(int index);
    /// <summary>
    /// Returns the position range spanned by the parse unit at the given index within this parse queue.
    /// This is accurate.
    /// </summary>
    public abstract PositionRange PositionOfObject(int index);

    public PositionRange PositionUpToCurrentObject => CurrentUnitIndex.Try(out var cu) ? PositionUpToObject(cu) : Position;
    public abstract Reflector.ReflCtx Ctx { get; }
    public string AsFileLink(Reflector.MethodSignature sig) => Ctx.AsFileLink(sig);
    public abstract IParseQueue NextChild();

    /// <summary>
    /// Get the index of the current unit, or null if the queue is complete.
    /// </summary>
    public abstract int? CurrentUnitIndex { get; }
    
    /// <summary>
    /// Get the parse unit at the current index.
    /// <br/>Returns null if the queue is empty.
    /// <br/>The index returned is the same as <see cref="CurrentUnitIndex"/>, however, this function will fail
    /// on <see cref="ParenParseQueue"/>.
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

    private string PrintOrEmpty() {
        var s = Print();
        if (string.IsNullOrWhiteSpace(s))
            return "<Empty parser text>";
        return s;
    }
    public ReflectionException OOBException() =>
        this.WrapThrow("The parser ran out of text to read at the end of the following:");
    public ReflectionException WrapThrow(string content, Exception? inner = null) => 
        new ReflectionException(PositionUpToCurrentObject, $"{content}\n\t{PrintOrEmpty().Replace("\n", "\n\t")}", inner);

    public string WrapThrowErrorString(string content) => $"{content}\n\t{PrintOrEmpty().Replace("\n", "\n\t")}";
    
    /// <summary>
    /// Format an exception that shows the contents of this queue, highlighting the object at the given index.
    /// </summary>
    public ReflectionException WrapThrowHighlight(int index, string content, Exception? inner = null) => 
        new ReflectionException(PositionUpToCurrentObject, PositionOfObject(index), $"{content}\n\t{PrintHighlight(index)}", inner);

    /// <summary>
    /// Creates an exception for when there is leftover text in the queue. (Note: you should probably call
    /// <see cref="HasLeftovers"/> first to check whether there is actually unparsed text.)
    /// </summary>
    public ReflectionException WrapThrowLeftovers(int index, string? content = null, Exception? inner = null) =>
        WrapThrowHighlight(index, content ?? "Found leftover text after parsing.", inner);

    private string LeftoverMsgForType(Type t) =>
        $"Successfully created an object of type {t.SimpRName()}, but then found extra text (in ≪≫). " +
        "This may be because you have forgotten to put a comma before the highlighted text.";

    /// <summary>
    /// Returns true if there are leftovers and an error should be thrown.
    /// <br/>If this returns true, you can pass the index to <see cref="WrapThrowLeftovers(int,string?,Exception?)"/>
    /// </summary>
    /// <returns></returns>
    public virtual bool HasLeftovers(out int index) {
        index = 0;
        return false;
    }


    public IAST WrapInErrorIfHasLeftovers(IAST basis, Type t, string? msg = null) {
        if (HasLeftovers(out var ind))
            return new AST.Failure(WrapThrowLeftovers(ind, msg ?? LeftoverMsgForType(t)), t) { Basis = basis };
        return basis;
    }
    
    
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
    public const string LINE_DELIM = "\n";

    public static readonly HashSet<string> ARR_EMPTY = new() {
        ".", "{}", "_"
    }; 
    public const string ARR_OPEN = "{";
    public const string ARR_CLOSE = "}";

    /// <summary>
    /// Returns the next non-newline word in the stream, but skips the line if it is a property declaration.
    /// </summary>
    /// <returns></returns>
    public abstract ParsedUnit.Str ScanNonProperty();

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

    public override PositionRange PositionUpToObject(int index) =>
        new PositionRange(Position.Start, index == 0 ? Position.Start : Items[index - 1].position.Start);
    public override PositionRange PositionOfObject(int index) => Items[index].position;
    public override IParseQueue NextChild() => 
        new PUListParseQueue(Items[childIndex++], Ctx);
    public override bool Empty => childIndex >= paren.Items.Length;
    public override bool IsNewline => false;

    public override int? CurrentUnitIndex => childIndex >= Items.Length ? null : childIndex;
    public override ParsedUnit? MaybeGetCurrentUnit(out int i) => 
        throw new Exception($"Cannot call {nameof(MaybeGetCurrentUnit)} on a parentheses parser");
    public override void Advance() => throw new Exception("Cannot call Advance on a parentheses parser");

    public override string Print() => paren.Print();
    public override string PrintHighlight(int ii) => 
        $"({string.Join(", ", paren.Items.Select((x,i) => (i == ii) ? $"≪{x.units.Print()}≫" : x.units.Print()))})";

    public override ParsedUnit.Str ScanNonProperty() =>
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
        if (ctx == null) {
            ctx = Ctx = new Reflector.ReflCtx();
            ctx.ParseProperties(this);
        } else Ctx = ctx;
    }

    public override PositionRange PositionUpToObject(int index) =>
        new PositionRange(Position.Start, index == 0 ? Position.Start : atoms[index - 1].Position.End);
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

    public override int? CurrentUnitIndex => Index >= atoms.Length ? null : Index;
    public override ParsedUnit? MaybeGetCurrentUnit(out int i) {
        for (i = Index; i < atoms.Length && atoms[i].TryAsString() == LINE_DELIM; ++i) { }
        return i < atoms.Length ? atoms[i] : (ParsedUnit?) null;
    }

    public override void Advance() {
        GetCurrentUnit(out var i);
        Index = i + 1;
    }

    public override bool HasLeftovers(out int index) {
        //this can get called during the initialization code, which creates a new ReflCtx, so Ctx can be null
        MaybeGetCurrentUnit(out index);
        return index != atoms.Length;
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

    public override ParsedUnit.Str ScanNonProperty() => ScanNonProperty(null);

    public ParsedUnit.Str ScanNonProperty(int? start) {
        int max = atoms.Length;
        var i = start ?? Index;
        for (; i < max && atoms[i].TryAsString() == LINE_DELIM; ++i) { }
        ThrowIfOOB(i);
        while (true) {
            if (atoms[i].Enforce(i, this).Item != SMParser.PROP_KW) 
                return atoms[i].Enforce(i, this);
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
    private readonly int initialIndex;
    public override PositionRange Position => new(startPosition, 
        //Using CurrentObject.End is critical to ensuring correct location reporting in language server
        root.CurrentUnitIndex.Try(out var cu) ? root.PositionOfObject(cu).End : root.Position.End);
    public override Reflector.ReflCtx Ctx => root.Ctx;

    private readonly bool allowPostAggregate;
    public override bool AllowPostAggregate => allowPostAggregate && root.AllowPostAggregate;
    

    public NonLocalPUListParseQueue(PUListParseQueue root, bool allowPostAggregate=false) {
        this.initialIndex = root.Index;
        this.root = root;
        this.startPosition =
            root.CurrentUnitIndex.Try(out var cu) ? root.PositionOfObject(cu).Start : root.Position.End;
        this.allowPostAggregate = allowPostAggregate;
    }

    public override bool IsNewline => root.IsNewline;
    public override PositionRange PositionUpToObject(int index) => root.PositionUpToObject(index);
    public override PositionRange PositionOfObject(int index) => root.PositionOfObject(index);
    public override IParseQueue NextChild() => root.NextChild();
    public override int? CurrentUnitIndex => root.CurrentUnitIndex;
    public override ParsedUnit? MaybeGetCurrentUnit(out int i) => root.MaybeGetCurrentUnit(out i);
    public override void Advance() => root.Advance();
    public override string Print() => root.atoms.Skip(initialIndex).Print();
    public override string PrintHighlight(int ii) => root.PrintHighlight(ii);
    public override ParsedUnit.Str ScanNonProperty() => root.ScanNonProperty();
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
    public static string Print(this IEnumerable<ParsedUnit> ParsedUnits) {
        var units = ParsedUnits.ToList();
        var numNls = units.Count(u => u.TryAsString() == IParseQueue.LINE_DELIM);
        if (numNls > 5) {
            var sb = new StringBuilder();
            var padNext = false;
            var nlCt = 0;
            foreach (var u in units) {
                if (u.TryAsString() == IParseQueue.LINE_DELIM) {
                    if (++nlCt == 3)
                        sb.Append("...");
                    else if (nlCt == numNls - 3 + 1) {
                        sb.Append("\n...");
                        padNext = false;
                        continue;
                    }
                }
                //display first three lines and last three lines
                if (nlCt < 3 || nlCt > numNls - 3) {
                    if (padNext) sb.Append(" ");
                    sb.Append(Print(u));
                    padNext = true;
                }
            }
            return sb.ToString();
        } else
            return string.Join(" ", units.Select(Print));
    }

    public static string Print(this ParsedUnit ParsedUnit) =>
        ParsedUnit switch {
            ParsedUnit.Str s => s.Item,
            ParsedUnit.Paren p => Print(p.Items),
            _ => ""
        };
}


}