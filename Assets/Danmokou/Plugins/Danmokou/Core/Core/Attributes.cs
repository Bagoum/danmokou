using System;
using JetBrains.Annotations;

namespace Danmokou.Core {

/// <summary>
/// Attribute marking a reflection method that is automatically applied if none other match.
/// </summary>
[AttributeUsage((AttributeTargets.Method))]
public class FallthroughAttribute : Attribute {
    public readonly int priority;

    public FallthroughAttribute(int priority=0) {
        this.priority = priority;
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

[AttributeUsage(AttributeTargets.Method)]
public class PASourceTypesAttribute : Attribute {
    public readonly Type[] types;
    public PASourceTypesAttribute(params Type[] types) {
        this.types = types;
    }
}
[AttributeUsage(AttributeTargets.Method)]
public class PAPriorityAttribute : Attribute {
    public readonly int priority;
    public PAPriorityAttribute(int priority) {
        this.priority = priority;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class WarnOnStrictAttribute : Attribute {
    public readonly int strictness;
    public WarnOnStrictAttribute(int strict = 1) {
        strictness = strict;
    }
}

[AttributeUsage(AttributeTargets.Parameter)]
public class LookupMethodAttribute : Attribute {
}
[AttributeUsage(AttributeTargets.Parameter)]
public class NonExplicitParameterAttribute : Attribute {
}

/// <summary>
/// On an assembly, Reflect marks that classes in the assembly should be examined for possible reflection.
/// <br/>If the assembly is marked, then on a class, Reflect marks that the methods in the class should be reflected.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class)]
public class ReflectAttribute : Attribute {
    public readonly Type? returnType;

    public ReflectAttribute(Type? returnType = null) {
        this.returnType = returnType;
    }
}

/// <summary>
/// On a GameObject or ScriptableObject field or property, Reflect marks that the field may be subject to an
///  .Into reflection call. If a type is provided, then the expression baker will treat the field as a string and
///  run .Into with the given type. Otherwise, it will simply load the field (eg. for properties that return
///  ReflWrap).
/// <br/>This attribute is not required, but expression baking may not work without it.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class ReflectIntoAttribute : Attribute {
    public readonly Type? resultType;
    public ReflectIntoAttribute(Type? resultType = null) {
        this.resultType = resultType;
    }
}

[AttributeUsage(AttributeTargets.Class)]
public class LocalizationStringsRepoAttribute : Attribute { }



}