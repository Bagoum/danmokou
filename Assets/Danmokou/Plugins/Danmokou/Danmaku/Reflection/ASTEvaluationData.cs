using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Danmokou.Reflection {

/// <summary>
/// Data passed top-down while evaluating an AST.
/// </summary>
public record ASTEvaluationData {
    public ImmutableHashSet<(Reflector.ExType, string)> ExposedVariables { get; init; } = ImmutableHashSet<(Reflector.ExType, string)>.Empty;

    public ASTEvaluationData AddExposed(IEnumerable<(Reflector.ExType, string)> exposed) => this with {
        ExposedVariables = ExposedVariables.Concat(exposed).ToImmutableHashSet()
    };
}
}