using System;
using BagoumLib;
using Mizuhashi;

namespace Danmokou.Core {
public class StaticException : Exception {
    public StaticException(string message) : base($"STATIC ERROR: {message}") { }
}

public class ReflectionException : Exception {
    public PositionRange Position { get; }
    public PositionRange? HighlightedPosition { get; init; }

    public ReflectionException(PositionRange pos, string message, Exception? inner = null) : base(message, inner) {
        this.Position = pos;
    }

    public Exception WithPositionInMessage() {
        var posStr = HighlightedPosition.Try(out var hp) ?
            $"{Position} ≪{hp}≫: " :
            $"{Position}: ";
        return new Exception(posStr + Message,
            InnerException is ReflectionException rex ? rex.WithPositionInMessage() : InnerException);
    }
}

public class BadTypeException : Exception {
    public BadTypeException(string message) : base(message) { }
    public BadTypeException(string message, Exception inner) : base(message, inner) { }
}

public class CompileException : Exception {
    public CompileException(string message) : base(message) { }
    public CompileException(string message, Exception inner) : base(message, inner) { }
}
}