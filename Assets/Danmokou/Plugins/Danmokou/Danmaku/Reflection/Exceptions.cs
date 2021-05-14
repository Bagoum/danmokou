using System;

namespace Danmokou.Reflection {
public static partial class Reflector {
    public class StaticException : Exception {
        public StaticException(string message) : base($"STATIC ERROR: {message}") { }
    }

    public class BadTypeException : Exception {
        public BadTypeException(string message) : base(message) { }
        public BadTypeException(string message, Exception inner) : base(message, inner) { }
    }

    public class CompileException : Exception {
        public CompileException(string message) : base(message) { }
        public CompileException(string message, Exception inner) : base(message, inner) { }
    }

    private class ParsingException : Exception {
        public ParsingException(string message) : base(message) { }
        public ParsingException(string message, Exception inner) : base(message, inner) { }
    }

    private class SMException : Exception {
        public SMException(string message) : base(message) { }
        public SMException(string message, Exception inner) : base(message, inner) { }
    }

    private class InvokeException : Exception {
        public InvokeException(string message, Exception inner) : base(message, inner) { }
    }
}
}