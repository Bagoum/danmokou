using System;

[AttributeUsage((AttributeTargets.Method))]
public class FallthroughAttribute : Attribute {
    public readonly int priority;
    public readonly bool upwardsCast;

    public FallthroughAttribute(int priority=0, bool upcast=false) {
        this.priority = priority;
        this.upwardsCast = upcast;
    }
}

[AttributeUsage(AttributeTargets.All)]
public class DontReflectAttribute : Attribute {
}
[AttributeUsage(AttributeTargets.Method, AllowMultiple=true)]
public class AliasAttribute : Attribute {
    public readonly string alias;

    public AliasAttribute(string alias) {
        this.alias = alias;
    }
}
[AttributeUsage(AttributeTargets.Method, AllowMultiple=true)]
public class GAliasAttribute : Attribute {
    public readonly string alias;
    public readonly Type type;

    public GAliasAttribute(Type t, string alias) {
        type = t;
        this.alias = alias;
    }
}