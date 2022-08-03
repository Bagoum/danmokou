using LanguageServer.VsCode.Contracts;
using Mizuhashi;
using Position = LanguageServer.VsCode.Contracts.Position;

namespace Danmokou.Reflection {
public static class AnalysisHelpers {
    public static Position ToPosition(this Mizuhashi.Position pos) =>
        new(pos.Line - 1, pos.Column - 1);

    public static Mizuhashi.Position ToDMKPosition(this Position pos, string source) =>
        new(source, pos.Line + 1, pos.Character + 1);
    public static Range ToRange(this PositionRange pos) =>
        new(pos.Start.ToPosition(), pos.End.ToPosition());

    public static PositionRange ToDMKRange(this Range r, string source) =>
        new(r.Start.ToDMKPosition(source), r.End.ToDMKPosition(source));
    
    public static Diagnostic ToDiagnostic(this LocatedParserError err, string source) => 
        new(DiagnosticSeverity.Error, 
            new PositionRange(new Mizuhashi.Position(source, err.Index), 
                new Mizuhashi.Position(source, source.Length)).ToRange(), "Parsing", err.Show(source));
}
}