using BagoumLib;
using JetBrains.Annotations;
using LanguageServer.VsCode.Contracts;
using Mizuhashi;
using Position = LanguageServer.VsCode.Contracts.Position;

namespace Danmokou.Reflection {
/// <summary>
/// Helpers consumed by the language server project.
/// </summary>
[PublicAPI]
public static class AnalysisHelpers {
    public static SymbolKind Symbol(this TypeMember mem) => mem switch {
        TypeMember.Constructor => SymbolKind.Constructor,
        TypeMember.Property => SymbolKind.Property,
        TypeMember.Field f => f.Fi.DeclaringType == f.Fi.FieldType && f.Fi.DeclaringType?.IsEnum is true ? 
            SymbolKind.Enum : SymbolKind.Field,
        _ => SymbolKind.Method,
    };
    
    public static Position ToPosition(this Mizuhashi.Position pos) =>
        new(pos.Line - 1, pos.Column - 1);

    public static Mizuhashi.Position ToDMKPosition(this Position pos, string source) =>
        new(source, pos.Line + 1, pos.Character + 1);

    public static PositionRange? ToRange(this IAST?[] asts) {
        if (asts.Length == 0) return null;
        Mizuhashi.Position? min = null;
        Mizuhashi.Position? max = null;
        for (int ii = 0; ii < asts.Length; ++ii) {
            if (asts[ii] is {} ast) {
                if (ast.Position.Start.Index < (min ??= ast.Position.Start).Index)
                    min = ast.Position.Start;
                if (ast.Position.End.Index > (max ??= ast.Position.End).Index)
                    max = ast.Position.End;
            }
        }
        return (min is { } _min && max is { } _max) ? new(_min, _max) : null;
    }
    public static Range ToRange(this PositionRange pos) =>
        new(pos.Start.ToPosition(), pos.End.ToPosition());

    public static PositionRange ToDMKRange(this Range r, string source) =>
        new(r.Start.ToDMKPosition(source), r.End.ToDMKPosition(source));
    
    public static Diagnostic ToDiagnostic<T>(this LocatedParserError err, InputStream<T> stream) => 
        new(DiagnosticSeverity.Error, 
            stream.TokenWitness.ToPosition(err.Index, stream.Source.Length).ToRange(), 
            "Parsing", stream.ShowAllFailures(err));
}
}