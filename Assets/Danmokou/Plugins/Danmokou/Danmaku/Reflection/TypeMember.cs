using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using LanguageServer.VsCode.Contracts;
using Ex = System.Linq.Expressions.Expression;

namespace Danmokou.Reflection {
public abstract class TypeMember {
    //includes instance type if this is instance
    /// <summary>
    /// The parameters to this member. If this is an instance member (eg. string.Length), then the first
    ///  element is the instance parameter.
    /// </summary>
    public abstract Reflector.NamedParam[] Params { get; }
    public abstract Type ReturnType { get; }
    public abstract bool Static { get; }
    public abstract MemberInfo BaseMi { get; }
    public string Name => BaseMi.Name;
    public string TypeName => BaseMi.ReflectedType!.SimpRName();
    
    public abstract SymbolKind Symbol { get; }
    
    public T? GetAttribute<T>() where T : Attribute => BaseMi.GetCustomAttribute<T>();
    public abstract object? InvokeInst(object? instance, params object?[] args);
#pragma warning disable CS8634
    public object? Invoke(params object?[] args) => InvokeInst(MaybeSplitInstance(ref args), args);
#pragma warning restore CS8634
    public abstract Ex InvokeExInst(Ex? instance, params Ex[] args);
    public Ex InvokeEx(params Ex[] args) => InvokeExInst(MaybeSplitInstance(ref args), args);

    public abstract string TypeOnlySignature();
    public string AsSignature() => AsSignature((t, _) => t.AsParameter);
    public abstract string AsSignature(Func<Reflector.NamedParam, int, string> paramMod);

    public static TypeMember Make(MemberInfo mi) =>
        MaybeMake(mi) ?? throw new ArgumentOutOfRangeException(mi.GetType().ToString());
    
    public static TypeMember? MaybeMake(MemberInfo mi) => mi switch {
        MethodInfo meth => new Method(meth),
        ConstructorInfo cons => new Constructor(cons),
        PropertyInfo pr => new Property(pr),
        FieldInfo f => new Field(f),
        _ => null
    };

    public T? MaybeSplitInstance<T>(ref T[] args) where T:class {
        if (Static) return null;
        var inst = args[0];
        args = args.Skip(1).ToArray();
        return inst;
    }
    
    public class Method : TypeMember {
        public MethodInfo Mi { get; init; }
        public override MemberInfo BaseMi => Mi;
        public override Reflector.NamedParam[] Params { get; }
        public override Type ReturnType => Mi.ReturnType;
        public override bool Static => Mi.IsStatic;
        public bool IsExtension { get; }
        public override SymbolKind Symbol => SymbolKind.Method;

        public Method(MethodInfo Mi) {
            this.Mi = Mi;
            Params = ParamsForMethod(Mi);
            IsExtension = Mi.GetCustomAttribute<ExtensionAttribute>() != null;
        }

        public static Reflector.NamedParam[] ParamsForMethod(MethodInfo mi) {
            var args = mi.GetParameters().Select(x => (Reflector.NamedParam)x);
            return (mi.IsStatic ? args : args.Prepend(new(mi.ReflectedType!, "Instance"))).ToArray();
        }

        public override object? InvokeInst(object? instance, params object?[] args) => Mi.Invoke(instance, args);
        public override Ex InvokeExInst(Ex? instance, params Ex[] args) => Ex.Call(instance, Mi, args);
        
        public override string TypeOnlySignature() {
            if (Params.Length == 0 || Params.Length == 1 && !Static)
                return ReturnType.SimpRName();
            return $"({string.Join(", ", Params.Skip(Static ? 0 : 1).Select(p => p.Type.SimpRName()))}): {ReturnType.SimpRName()}";
        }

        public override string AsSignature(Func<Reflector.NamedParam, int, string> paramMod) => Static ?
            $"{ReturnType.SimpRName()} {Mi.Name}({string.Join(", ", Params.Select(paramMod))})" :
            $"{ReturnType.SimpRName()} {Params[0].Type.SimpRName()}.{Mi.Name}({string.Join(", ", Params.Select(paramMod).Skip(Static ? 0 : 1))})";

    }

    public class Constructor : TypeMember {
        public ConstructorInfo Cons { get; init; }
        public override MemberInfo BaseMi => Cons;
        public override Reflector.NamedParam[] Params { get; }
        public override Type ReturnType => Cons.DeclaringType!;
        public override bool Static => true;
        public override SymbolKind Symbol => SymbolKind.Constructor;
        public Constructor(ConstructorInfo Cons) {
            this.Cons = Cons;
            Params = Cons.GetParameters().Select(x => (Reflector.NamedParam)x).ToArray();
        }

        public override object InvokeInst(object? instance, params object?[] args) => Cons.Invoke(args);
        public override Ex InvokeExInst(Ex? instance, params Ex[] args) => Ex.New(Cons, args);
        
        public override string TypeOnlySignature() => Params.Length == 0 ? "" : 
                $"({string.Join(", ", Params.Select(p => p.Type.SimpRName()))})";

        public override string AsSignature(Func<Reflector.NamedParam, int, string> paramMod) =>
            $"new {TypeName}({string.Join(", ", Params.Select(paramMod))})";
    }

    public class Property : TypeMember {
        public PropertyInfo Prop { get; init; }
        public override MemberInfo BaseMi => Prop;
        public override Reflector.NamedParam[] Params { get; }
        public override Type ReturnType { get; }
        public override bool Static { get; }
        public override SymbolKind Symbol => SymbolKind.Property;
        public Property(PropertyInfo Prop) {
            this.Prop = Prop;
            var getter = Prop.GetMethod!;
            Params = Method.ParamsForMethod(getter);
            ReturnType = getter.ReturnType;
            Static = getter.IsStatic;
        }

        public override object? InvokeInst(object? instance, params object?[] args) =>
            args.Length > 0 ? Prop.GetValue(instance, args) : Prop.GetValue(instance);

        public override Ex InvokeExInst(Ex? instance, params Ex[] args) =>
            //these return different expressions, so we can't just use the first call
            args.Length > 0 ? Ex.Property(instance, Prop, args) : Ex.Property(instance, Prop);

        public override string TypeOnlySignature() => ReturnType.SimpRName();
        public override string AsSignature(Func<Reflector.NamedParam, int, string> paramMod) =>
            $"{ReturnType.SimpRName()} {TypeName}.{Prop.Name}";
    }

    public class Field : TypeMember {
        public FieldInfo Fi { get; init; }
        public override MemberInfo BaseMi => Fi;
        public override Reflector.NamedParam[] Params { get; }
        public override Type ReturnType { get; }
        public override bool Static { get; }
        public override SymbolKind Symbol => Fi.DeclaringType == Fi.FieldType && Fi.DeclaringType?.IsEnum is true ? 
            SymbolKind.Enum : SymbolKind.Field;
        public Field(FieldInfo Fi) {
            this.Fi = Fi;
            Params = Fi.IsStatic ? 
                Array.Empty<Reflector.NamedParam>() : 
                new[] { new Reflector.NamedParam(Fi.ReflectedType!, "Instance") };
            ReturnType = Fi.FieldType;
            Static = Fi.IsStatic;
        }

        public override object? InvokeInst(object? instance, params object?[] args) => Fi.GetValue(instance);
        public override Ex InvokeExInst(Ex? instance, params Ex[] args) => Ex.Field(instance, Fi);

        public override string TypeOnlySignature() => ReturnType.SimpRName();
        public override string AsSignature(Func<Reflector.NamedParam, int, string> paramMod) =>
            $"{ReturnType.SimpRName()} {TypeName}.{Fi.Name}";
    }

}


}