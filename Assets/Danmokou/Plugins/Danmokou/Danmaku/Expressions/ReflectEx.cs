using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using UnityEngine;
using System;
using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Expressions;
using BagoumLib.Reflection;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DataHoist;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Reflection;
using Danmokou.Reflection2;
using JetBrains.Annotations;
using Ex = System.Linq.Expressions.Expression;
using static Danmokou.Expressions.ExUtils;
using ParameterExpression = System.Linq.Expressions.ParameterExpression;
using Parser = Danmokou.DMath.Parser;


namespace Danmokou.Expressions {
public static class ReflectEx {


    public static TEx<T> Let1<T, L>(string alias, Func<TExArgCtx, TEx<L>> content, Func<TEx<T>> inner, TExArgCtx applier) {
        var variabl = V<L>();
        using var let = applier.Let(alias, variabl);
        return Ex.Block(new[] {variabl},
            Ex.Assign(variabl, content(applier)),
            inner()
        );
    }

    public readonly struct Alias {
        /// <summary>
        /// Type of the aliased variable, on the level of typeof(float)
        /// </summary>
        public readonly Type type;
        public readonly string alias;
        public readonly Func<TExArgCtx, TEx> func;
        public bool DirectAssignment { get; init; }

        public Alias(Type type, string alias, Func<TExArgCtx, TEx> func) {
            this.type = type;
            this.alias = alias;
            this.func = func;
            this.DirectAssignment = false;
        }
    }
    
    public static Ex Let<L>((string alias, Func<TExArgCtx, TEx<L>> content)[] aliases, Func<Ex> inner, TExArgCtx applier) {
        var stmts = new Ex[aliases.Length + 1];
        var vars = new ParameterExpression[aliases.Length];
        var lets = new List<TExArgCtx.LocalLet>();
        for (int ii = 0; ii < aliases.Length; ++ii) {
            Ex alias_value = aliases[ii].content(applier);
            lets.Add(applier.Let(aliases[ii].alias, vars[ii] = 
                V(alias_value.Type, applier.Ctx.NameWithSuffix(aliases[ii].alias))));
            stmts[ii] = Ex.Assign(vars[ii], alias_value);
        }
        stmts[aliases.Length] = inner();
        for (int ii = lets.Count - 1; ii >= 0; --ii)
            lets[ii].Dispose();
        return Ex.Block(vars, stmts);
    }
    
    public static Ex LetAlias(IEnumerable<Alias> aliases, Func<Ex> inner, TExArgCtx applier) {
        var stmts = new List<Ex>();
        var vars = new List<ParameterExpression>();
        var lets = new List<TExArgCtx.LocalLet>();
        foreach (var a in aliases) {
            Ex alias_value = a.func(applier);
            if (a.DirectAssignment) {
                lets.Add(applier.Let(a.alias, alias_value));
            } else {
                var tempVar = V(alias_value.Type, a.alias);
                vars.Add(tempVar);
                lets.Add(applier.Let(a.alias, tempVar));
                stmts.Add(Ex.Assign(tempVar, alias_value));
            }
        }
        stmts.Add(inner());
        for (int ii = lets.Count - 1; ii >= 0; --ii)
            lets[ii].Dispose();
        return Ex.Block(vars, stmts);
    }

    public static Ex SetAlias(Alias[] aliases, Func<Ex> inner, TExArgCtx tac) {
        var stmts = new List<Ex>();
        foreach (var a in aliases) {
            stmts.Add(PIData.SetValue(tac, a.type, a.alias, a.func));
        }
        stmts.Add(inner());
        return Ex.Block(stmts);
    }

    public static Expression? GetAliasFromStack(string alias, TExArgCtx tac) {
        if (tac.Ctx.AliasStack.TryGetValue(alias, out var f))
            return f.Peek();
        return null;
    }
    
    /// <summary>
    /// Get the value of a variable that may be provided as an alias (<see cref="Let{L}"/>),
    ///  as a GCX variable, as a variable bound to the PICustomData for a bullet, or as a function argument.
    /// <br/>T is on the level of typeof(float).
    /// </summary>
    public static Ex ReferenceExpr<T>(string alias, TExArgCtx tac, TEx<T>? deflt = null) {
        if (alias[0] == Parser.SM_REF_KEY_C) alias = alias.Substring(1); //Important for reflector handling of &x
        bool isExplicit = alias.StartsWith(".");
        if (isExplicit)
            alias = alias.Substring(1);
        //Standard method, used by Let and GCXPath.expose (which internally compiles to Let) 
        if (GetAliasFromStack(alias, tac) is { } ex)
            return ex;
        if (tac.MaybeGetByName<T>(alias).Try(out var prm))
            return prm;
        //variables in EnvFrame (GCXU or GCXF)
        if (tac.Ctx.Scope.TryGetLocalOrParentVariable(tac, typeof(T), alias, out var decl, out var p) is { } aex) {
            return aex;
        }
        //In functions not scoped by the GCX (eg. bullet controls)
        //The reason for using the special marker is that we cannot give good errors if an incorrect value is entered
        //(good error handling would make lookup slower, and this is hotpath),
        //so we need to make opting into this completely explicit. 
        if ((isExplicit || deflt != null) && tac.MaybeBPI != null) {
            try {
                return PIData.GetIfDefined<T>(tac, alias, deflt is null ? null : (Ex)deflt);
            } catch (Exception) {
                //pass
            }
        }
        var maybe_outofscope = isExplicit ?
            "" :
            "\n\tIf you are defining a bullet control or some other unscoped function, " +
            "then you may need to make the reference explicit by prefixing it with \"&.\" instead of \"&\"";
        throw new CompileException(
            $"The reference {alias} is used, but does not have a value.{maybe_outofscope}");
    }

    public static Func<TExArgCtx, TEx<T>> ReferenceExpr<T>(string alias) => bpi => ReferenceExpr<T>(alias, bpi);

    
    //TODO consider replacing SafeResizeable here with a dictionary
    
    /// <summary>
    /// A variable that can be saved and read between different bullets in public data hoisting as long as they use the same indexer value.
    /// </summary>
    public readonly struct Hoist<T> {
        private readonly string name;
        private readonly SafeResizableArray<T> data;
        public Hoist(string name) {
            data = PublicDataHoisting.Register<T>(this.name = name);
        }

        public void Save(int index, T value) => data.SafeAssign(index, value);
        public T Retrieve(int index) => data.SafeGet(index);

        private void Bake(TExArgCtx tac) {
#if EXBAKE_SAVE
            var key_name = tac.Ctx.NameWithSuffix("pubHoist");
            var key_assign = FormattableString.Invariant(
                $"var {key_name} = PublicDataHoisting.Register<{typeof(T)}>(\"{name}\");");
            tac.Ctx.HoistedVariables.Add(key_assign);
            tac.Ctx.HoistedReplacements[Ex.Constant(data)] = Ex.Variable(typeof(SafeResizableArray<T>), key_name);
#endif
        }

        private static readonly ExFunction safeAssign =
            ExFunction.Wrap<SafeResizableArray<T>>("SafeAssign", new[] {typeof(int), typeof(T)});
        private static readonly ExFunction safeGet = ExFunction.Wrap<SafeResizableArray<T>, int>("SafeGet");

        public Ex Save(Ex index, Ex val, TExArgCtx tac) {
            Bake(tac);
            return safeAssign.InstanceOf(Ex.Constant(data), Ex.Convert(index, tint), val);
        }

        public Ex Retrieve(Ex index, TExArgCtx tac) {
            Bake(tac);
            return safeGet.InstanceOf(Ex.Constant(data), Ex.Convert(index, tint));
        }

        public override string ToString() => $"{name}<{typeof(T).RName()}>";
    }
}

/// <summary>
/// Where X is a variable bound in a let statement,
/// this is a string of the form X(.Y)*
/// that retrieves the arbitrarily nested member of X.
/// This member is both readable and writeable.
/// </summary>
public readonly struct ReferenceMember {
    public readonly string var;
    public readonly IReadOnlyList<string> members;

    public ReferenceMember(string raw) {
        int firstPeriod = raw.IndexOf('.');
        if (firstPeriod == -1) {
            var = raw;
            members = new string[0];
        } else {
            var = raw.Substring(0, firstPeriod);
            members = raw.Substring(firstPeriod + 1).Split('.');
        }
    }

    public override string ToString() => $"{var}.{String.Join(".", members)}";

    private void Precheck(GCOperator op) {
        /*if (!data.ContainsKey(var) && members.Count > 0)
            throw new Exception($"Can't write members when value does not exist: {this}");
        if (!data.ContainsKey(var) && op != GCOperator.Assign)
            throw new Exception($"New variable {this} can only be assigned, but was given operator {op}");*/
        if (members.Count > 1) throw new Exception($"Can't write to members two layers deep: {this}");
    }

    private static void ResolveFloat(ref float src, float other, GCOperator op) {
        if (op == GCOperator.AddAssign) src += other;
        else if (op == GCOperator.MulAssign) src *= other;
        else if (op == GCOperator.SubAssign) src -= other;
        else if (op == GCOperator.DivAssign) src /= other;
        else if (op == GCOperator.FDivAssign) src = Mathf.Floor(src / other);
        else src = other;
    }

    public void Resolve(ref float variable, float assigned, GCOperator op) {
        if (members.Count > 0) throw new Exception($"Float value {this} has no members.");
        Precheck(op);
        ResolveFloat(ref variable, assigned, op);
    }

    public void ResolveMembers(ref Vector2 variable, Vector2 assigned, GCOperator op) {
        Precheck(op);
        if (members.Count > 0) throw new Exception($"Can't assign V2 to V2 member {this}");
        ResolveFloat(ref variable.x, assigned.x, op);
        ResolveFloat(ref variable.y, assigned.y, op);
    }

    public void ResolveMembers(ref Vector2 variable, float assigned, GCOperator op) {
        Precheck(op);
        if (members.Count == 0) throw new Exception($"Can't assign float to V2 {this}");
        if (members[0] == "x") ResolveFloat(ref variable.x, assigned, op);
        else if (members[0] == "y") ResolveFloat(ref variable.y, assigned, op);
        else throw new Exception($"Can't get V2.f member {members[0]}");
    }

    public Vector3 ResolveMembers(ref Vector3 variable, Vector3 assigned, GCOperator op) {
        Precheck(op);
        if (members.Count > 0) throw new Exception($"Can't assign V3 to V3 member {this}");
        ResolveFloat(ref variable.x, assigned.x, op);
        ResolveFloat(ref variable.y, assigned.y, op);
        ResolveFloat(ref variable.z, assigned.z, op);
        return variable;
    }

    public Vector3 ResolveMembers(ref Vector3 variable, Vector2 assigned, GCOperator op) {
        Precheck(op);
        if (members.Count == 0) throw new Exception($"Can't assign V2 to V3 {this}");
        if (members[0] == "xy") {
            ResolveFloat(ref variable.x, assigned.x, op);
            ResolveFloat(ref variable.y, assigned.y, op);
        } else if (members[0] == "yz") {
            ResolveFloat(ref variable.y, assigned.x, op);
            ResolveFloat(ref variable.z, assigned.y, op);
        } else if (members[0] == "xz") {
            ResolveFloat(ref variable.x, assigned.x, op);
            ResolveFloat(ref variable.z, assigned.y, op);
        } else throw new Exception($"Can't get V3.V2 member {members[0]}");
        return variable;
    }

    public Vector3 ResolveMembers(ref Vector3 variable, float assigned, GCOperator op) {
        Precheck(op);
        if (members.Count == 0) throw new Exception($"Can't assign float to V3 {this}");
        if (members[0] == "x") ResolveFloat(ref variable.x, assigned, op);
        else if (members[0] == "y") ResolveFloat(ref variable.y, assigned, op);
        else if (members[0] == "z") ResolveFloat(ref variable.z, assigned, op);
        else throw new Exception($"Can't get V3.f member {members[0]}");
        return variable;
    }

    public V2RV2 ResolveMembers(ref V2RV2 variable, V2RV2 assigned, GCOperator op) {
        Precheck(op);
        if (members.Count > 0) throw new Exception($"Can't assign RV2 to RV2 member {this}");
        MutV2RV2 rv2 = variable;
        ResolveFloat(ref rv2.nx, assigned.nx, op);
        ResolveFloat(ref rv2.ny, assigned.ny, op);
        ResolveFloat(ref rv2.rx, assigned.rx, op);
        ResolveFloat(ref rv2.ry, assigned.ry, op);
        ResolveFloat(ref rv2.angle, assigned.angle, op);
        return variable = rv2;
    }

    public V2RV2 ResolveMembers(ref V2RV2 variable, float assigned, GCOperator op) {
        Precheck(op);
        if (members.Count == 0) throw new Exception($"Can't assign float to V2RV2 {this}");
        MutV2RV2 rv2 = variable;
        if (members[0] == "nx") ResolveFloat(ref rv2.nx, assigned, op);
        else if (members[0] == "ny") ResolveFloat(ref rv2.ny, assigned, op);
        else if (members[0] == "rx") ResolveFloat(ref rv2.rx, assigned, op);
        else if (members[0] == "ry") ResolveFloat(ref rv2.ry, assigned, op);
        else if (members[0].StartsWith("a")) ResolveFloat(ref rv2.angle, assigned, op);
        else throw new Exception($"Can't get RV2.f member {members[0]}");
        return variable = rv2;
    }

    public V2RV2 ResolveMembers(ref V2RV2 variable, Vector2 assigned, GCOperator op) {
        Precheck(op);
        if (members.Count == 0) throw new Exception($"Can't assign V2 to V2RV2 {this}");
        MutV2RV2 rv2 = variable;
        if (members[0] == "rxy") {
            ResolveFloat(ref rv2.rx, assigned.x, op);
            ResolveFloat(ref rv2.ry, assigned.y, op);
        } else if (members[0] == "nxy") {
            ResolveFloat(ref rv2.nx, assigned.x, op);
            ResolveFloat(ref rv2.ny, assigned.y, op);
        } else throw new Exception($"Can't get RV2.V2 member {members[0]}");
        return variable = rv2;
    }

    public static implicit operator ReferenceMember(string s) => new(s);
}

}
