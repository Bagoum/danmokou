using System;
using BagoumLib;
using Mizuhashi;
using Position = UnityEngine.UIElements.Position;

namespace Danmokou.Core {
public class StaticException : Exception {
    public StaticException(string message) : base($"STATIC ERROR: {message}") { }
}

public class ReflectionException : Exception {
    public PositionRange Position { get; }
    public PositionRange? HighlightedPosition { get; }
    public string MessageWithoutPosition { get; }

    public ReflectionException(PositionRange pos, string message, Exception? inner = null) : base($"{pos}: {message}", inner) {
        this.MessageWithoutPosition = message;
        this.Position = pos;
    }
    
    public ReflectionException(PositionRange pos, PositionRange highlighted, string message, Exception? inner = null) : 
        base($"{pos} ≪{highlighted}≫: {message}", inner) {
        this.MessageWithoutPosition = message;
        this.Position = pos;
        this.HighlightedPosition = highlighted;
    }

    public static ReflectionException Make(PositionRange pos, PositionRange? highlighted, string message,
        Exception? inner) =>
        highlighted.Try(out var hp) ?
            new ReflectionException(pos, hp, message, inner) :
            new ReflectionException(pos, message, inner);

    public ReflectionException Copy(Exception newInner) =>
        ReflectionException.Make(Position, HighlightedPosition, MessageWithoutPosition, newInner);
    
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