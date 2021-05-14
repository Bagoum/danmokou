using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace Danmokou.DMath {
public abstract class MultiOp {
    public enum Priority {
        ALL = 0,
        CLEAR_SCENE = 100,
        CLEAR_PHASE = 200
    }
}
public abstract class MultiOp<T> : MultiOp {
    public T Value { get; private set; }
    private readonly HashSet<Token> tokens = new HashSet<Token>();
    private readonly Action<T>? onChange;

    protected abstract T AddToken(T current, T token);
    protected abstract T RemoveToken(T current, T token);

    public MultiOp(T value, Action<T>? onChange) {
        Value = value;
        this.onChange = onChange;
    }

    private void Changed() {
        this.onChange?.Invoke(Value);
    }

    public void RevokeAll(Priority minPriority) {
        foreach (var t in tokens.ToArray()) {
            if (t.priority >= minPriority) TryRevoke(t);
        }
    }
    public bool TryRevoke(Token t) {
        if (tokens.Contains(t)) {
            tokens.Remove(t);
            Value = RemoveToken(Value, t.value);
            Changed();
            return true;
        } else return false;
    }

    public Token CreateModifier(T m, Priority p) {
        Value = AddToken(Value, m);
        Changed();
        var t = new Token(this, p, m);
        tokens.Add(t);
        return t;
    }

    public class Token {
        public readonly Priority priority;
        public readonly T value;
        private readonly MultiOp<T> source;

        public Token(MultiOp<T> s, Priority p, T m) {
            priority = p;
            value = m;
            source = s;
        }

        public bool TryRevoke() => source.TryRevoke(this);
    }
    
}

public class MultiMultiplier : MultiOp<float> {
    public MultiMultiplier(float value, Action<float>? onChange) : base(value, onChange) { }
    protected override float AddToken(float current, float token) => current * token;

    protected override float RemoveToken(float current, float token) => current / token;
}
public class MultiMultiplierD : MultiOp<double> {
    public MultiMultiplierD(double value, Action<double>? onChange) : base(value, onChange) { }
    protected override double AddToken(double current, double token) => current * token;

    protected override double RemoveToken(double current, double token) => current / token;
}

public class MultiAdder : MultiOp<int> {
    public MultiAdder(int value, Action<int>? onChange) : base(value, onChange) { }
    protected override int AddToken(int current, int token) => current + token;

    protected override int RemoveToken(int current, int token) => current - token;

    public Token CreateToken1(Priority p) => CreateModifier(1, p);
}

}