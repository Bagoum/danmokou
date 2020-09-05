using System;

/// <summary>
/// Attribute marking a reflection method that is automatically applied if none other match.
/// </summary>
[AttributeUsage((AttributeTargets.Method))]
public class FallthroughAttribute : Attribute {
    public readonly int priority;
    public readonly bool upwardsCast;

    public FallthroughAttribute(int priority=0, bool upcast=false) {
        this.priority = priority;
        this.upwardsCast = upcast;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class ExprCompilerAttribute : Attribute {
    
}

[AttributeUsage(AttributeTargets.All)]
public class DontReflectAttribute : Attribute {
}
/// <summary>
/// Attribute marking a reflection alias for this method.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple=true)]
public class AliasAttribute : Attribute {
    public readonly string alias;

    public AliasAttribute(string alias) {
        this.alias = alias;
    }
}
/// <summary>
/// Attribute marking a reflection alias for a generic variant of this method.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple=true)]
public class GAliasAttribute : Attribute {
    public readonly string alias;
    public readonly Type type;

    public GAliasAttribute(Type t, string alias) {
        type = t;
        this.alias = alias;
    }
}