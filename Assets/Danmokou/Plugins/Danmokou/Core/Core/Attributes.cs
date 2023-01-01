using System;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Core {

/// <summary>
/// Attribute marking a reflection method that is automatically applied if none other match.
/// </summary>
[AttributeUsage((AttributeTargets.Method))]
public class FallthroughAttribute : Attribute {
    //TODO you probably don't need priority anymore
    public readonly int priority;

    public FallthroughAttribute(int priority=0) {
        this.priority = priority;
    }
}

/// <summary>
/// Attribute marking a reflection method that is automatically applied if none other match, and which
///  converts an expression type into a real type (eg. ExTP to TP).
/// <br/>Must occur together with <see cref="FallthroughAttribute"/>.
/// </summary>
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

    public GAliasAttribute(string alias, Type t) {
        type = t;
        this.alias = alias;
    }
}

/// <summary>
/// Marks that the result of this function is a GCXU whose exposed variables can be extended during AST realization.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ExtendGCXUExposedAttribute : Attribute { }

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

public class FileLinkAttribute : Attribute {
    public readonly string file;
    public readonly string member;
    public readonly int line;

    public FileLinkAttribute([CallerFilePath] string file = "",
        [CallerMemberName] string member = "",
        [CallerLineNumber] int line = 0) {
        this.file = file;
        this.member = member;
        this.line = line;
    }
    
    public string FileLink(string? content = null) => LogUtils.ToFileLink(file, line, content);
}

/// <summary>
/// On an assembly, Reflect marks that classes in the assembly should be examined for possible reflection.
/// <br/>If the assembly is marked, then on a class, Reflect marks that the methods in the class should be reflected.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class)]
public class ReflectAttribute : FileLinkAttribute {
    public readonly Type? returnType;
    public ReflectAttribute(Type? returnType = null, 
        [CallerFilePath] string file = "",
        [CallerMemberName] string member = "",
        // ReSharper disable ExplicitCallerInfoArgument
        [CallerLineNumber] int line = 0) : base(file, member, line) {
        // ReSharper restore ExplicitCallerInfoArgument
        this.returnType = returnType;
    }
}

/// <summary>
/// The marked method is a BDSL2 operator. It should not be permitted for reflection in BDSL2.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class BDSL2OperatorAttribute : Attribute { }

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


//INFORMATIONAL ATTRIBUTES
//These attributes are used to provide richer semantic information for the
// DMK language server.
//It's not a big deal if they're missing on some methods.

/// <summary>
/// (Informational) The marked method is, semantically speaking, an operator.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class OperatorAttribute : Attribute { }

/// <summary>
/// (Informational) The marked method is an "atomic" reflection method,
///  ie. it does not recurse on higher-order functions.
/// For example, "hpi" does not recurse, but "lerp" does recurse.
/// <br/>When marking a class, it means that all methods in the class
/// are atomic.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class AtomicAttribute : Attribute { }




}