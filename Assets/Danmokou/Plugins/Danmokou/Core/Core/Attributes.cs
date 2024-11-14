using System;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Core {

/// <summary>
/// Attribute marking a reflection alias for a generic variant of this method.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple=true)]
public class GAliasAttribute : Attribute {
    public readonly string alias;
    public readonly Type type;
    public readonly bool reflectOriginal;

    public GAliasAttribute(string alias, Type t, bool reflectOriginal = true) {
        type = t;
        this.alias = alias;
        this.reflectOriginal = reflectOriginal;
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

/// <summary>
/// A parameter that should be reflected by looking for a method by name and returning it as a lambda.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public class LookupMethodAttribute : Attribute {
}

/// <summary>
/// A parameter that should be reflected by specialized type-specific rules instead of reading text.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public class NonExplicitParameterAttribute : Attribute {
}

/// <summary>
/// A StateMachine[] parameter that should be realized by
///  parsing as many state machine children as possible according to SM parenting rules,
///  without looking for array {} markers.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public class BDSL1ImplicitChildrenAttribute : Attribute {
}

/// <summary>
/// The marked method can only be reflected in BDSL1.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class BDSL1OnlyAttribute : Attribute { }

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
public class GeneratedExpressionsAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class)]
public class LocalizationStringsRepoAttribute : Attribute { }


//INFORMATIONAL ATTRIBUTES
//These attributes are used to provide richer semantic information for the
// DMK language server.
//It's not a big deal if they're missing on some methods.

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