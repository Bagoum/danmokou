using System.Collections.Generic;
using System.Linq;

namespace DMath {
public class MultiMultiplier {
    public enum Priority {
        CLEAR_SCENE = 100,
        CLEAR_PHASE = 200
    }
    public float Value { get; private set; }
    private readonly HashSet<Token> tokens = new HashSet<Token>();

    public MultiMultiplier(float value) {
        Value = value;
    }

    public void RevokeAll(Priority minPriority) {
        foreach (var t in tokens.ToArray()) {
            if (t.priority >= minPriority) TryRevoke(t);
        }
    }
    public bool TryRevoke(Token t) {
        if (tokens.Contains(t)) {
            tokens.Remove(t);
            Value /= t.multiplier;
            return true;
        } else return false;
    }

    public Token CreateMultiplier(float m, Priority p = Priority.CLEAR_PHASE) {
        Value *= m;
        var t = new Token(this, p, m);
        tokens.Add(t);
        return t;
    }

    public class Token {
        public readonly Priority priority;
        public readonly float multiplier;
        private readonly MultiMultiplier source;

        public Token(MultiMultiplier s, Priority p, float m) {
            priority = p;
            multiplier = m;
            source = s;
        }

        public bool TryRevoke() => source.TryRevoke(this);
    }
    
}
}