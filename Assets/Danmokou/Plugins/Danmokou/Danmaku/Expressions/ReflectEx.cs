using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using UnityEngine;
using System;
using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Expressions;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DataHoist;
using Danmokou.DMath;
using Danmokou.Reflection;
using JetBrains.Annotations;
using Ex = System.Linq.Expressions.Expression;
using static Danmokou.Expressions.ExUtils;


namespace Danmokou.Expressions {
public static class ReflectEx {


    public static TEx<T> Let<T, L>(string alias, Func<TExArgCtx, TEx<L>> content, Func<TEx<T>> inner, TExArgCtx applier) {
        var variabl = V<L>();
        using var let = applier.Let(alias, variabl);
        return Ex.Block(new[] {variabl},
            Ex.Assign(variabl, content(applier)),
            inner()
        );
    }

    public readonly struct Alias {
        public readonly string alias;
        public readonly Func<TExArgCtx, TEx> func;

        public Alias(string alias, Func<TExArgCtx, TEx> func) {
            this.alias = alias;
            this.func = func;
        }
    }

    public static Ex Let2(Alias[] aliases, Func<Ex> inner, TExArgCtx applier) {
        var stmts = new Ex[aliases.Length + 1];
        var vars = new ParameterExpression[aliases.Length];
        var lets = new List<IDisposable>();
        for (int ii = 0; ii < aliases.Length; ++ii) {
            Ex alias_value = aliases[ii].func(applier);
            lets.Add(applier.Let(aliases[ii].alias, vars[ii] =
                V(alias_value.Type, applier.Ctx.NameWithSuffix(aliases[ii].alias))));
            stmts[ii] = Ex.Assign(vars[ii], alias_value);
        }
        stmts[aliases.Length] = inner();
        for (int ii = 0; ii < lets.Count; ++ii)
            lets[ii].Dispose();
        return Ex.Block(vars, stmts);
    }

    public static Ex Let<L>((string alias, Func<TExArgCtx, TEx<L>> content)[] aliases, Func<Ex> inner, TExArgCtx applier) {
        var stmts = new Ex[aliases.Length + 1];
        var vars = new ParameterExpression[aliases.Length];
        var lets = new List<IDisposable>();
        for (int ii = 0; ii < aliases.Length; ++ii) {
            Ex alias_value = aliases[ii].content(applier);
            lets.Add(applier.Let(aliases[ii].alias, vars[ii] = 
                V(alias_value.Type, applier.Ctx.NameWithSuffix(aliases[ii].alias))));
            stmts[ii] = Ex.Assign(vars[ii], alias_value);
        }
        stmts[aliases.Length] = inner();
        for (int ii = 0; ii < lets.Count; ++ii)
            lets[ii].Dispose();
        return Ex.Block(vars, stmts);
    }
/*
    private static TEx<T> LetLambda<WF, T, L>((string alias, string[] args, Func<WF, TEx<L>> content)[] lambdas,
        Func<TEx<T>> inner, Dictionary<string, (string[], Func<WF, TEx<L>>)> stack) {
        
    }*/

    public interface ICompileReferenceResolver {
        bool TryResolve<T>(string alias, out Ex ex);
    }

    //T is on the level of typeof(float)
    public static Ex ReferenceExpr<T>(string alias, TExArgCtx tac, TEx<T>? deflt = null) {
        if (alias[0] == Parser.SM_REF_KEY_C) alias = alias.Substring(1); //Important for reflector handling of &x
        bool isExplicit = alias.StartsWith(".");
        if (isExplicit)
            alias = alias.Substring(1);
        //Standard method, used by Let and GCXPath.expose (which internally compiles to Let) 
        if (tac.Ctx.AliasStack.TryGetValue(alias, out var f))
            return f.Peek();
        //Used by GCXF<T>, ie. when a fixed GCX exists. This is for slower pattern expressions
        if (tac.MaybeGetByExprType<TExGCX>(out _).Try(out var gcx))
            return gcx.FindReference<T>(alias);
        if (tac.MaybeGetByName<T>(alias).Try(out var prm))
            return prm;
        //Automatic GCXPath.expose resolution
        if (tac.Ctx.ICRR != null && tac.Ctx.ICRR.TryResolve<T>(alias, out Ex ex))
            return ex;
        //In functions not scoped by the GCX (eg. bullet controls)
        //The reason for using the special marker is that we cannot give good errors if an incorrect value is entered
        //(good error handling would make lookup slower, and this is hotpath),
        //so we need to make opting into this completely explicit. 
        if ((isExplicit || deflt != null) && tac.MaybeBPI != null) {
            try {
                return FiringCtx.GetValue<T>(tac, alias, deflt);
            } catch (Exception) {
                //pass
            }
        }
        var maybe_outofscope = isExplicit ?
            "" :
            "\n\tIf you are defining a bullet control or some other unscoped function, " +
            "then you may need to make the reference explicit by prefixing it with \"&.\" instead of \"&\".";
        throw new Reflector.CompileException(
            $"The reference {alias} is used, but does not have a value.{maybe_outofscope}");
    }

    public static Func<TExArgCtx, TEx<T>> ReferenceLet<T>(string alias) => bpi => ReferenceExpr<T>(alias, bpi);

    //TODO consider replacing SafeResizeable here with a dictionary
    public readonly struct Hoist<T> {
        private readonly SafeResizableArray<T> data;

        public Hoist(string name) {
            data = PublicDataHoisting.Register<T>(name);
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

    private void Precheck<T>(IReadOnlyDictionary<string, T> data, GCOperator op) {
        if (!data.ContainsKey(var) && members.Count > 0)
            throw new Exception($"Can't write members when value does not exist: {this}");
        if (!data.ContainsKey(var) && op != GCOperator.Assign)
            throw new Exception($"New variable {this} can only be assigned, but was given operator {op}");
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

    public float Resolve(IReadOnlyDictionary<string, float> data, float assigned, GCOperator op) {
        if (members.Count > 0) throw new Exception($"Float value {this} has no members.");
        Precheck(data, op);
        if (op == GCOperator.Assign) return assigned;
        float x = data[var];
        ResolveFloat(ref x, assigned, op);
        return x;
    }

    public Vector2 ResolveMembers(IReadOnlyDictionary<string, Vector2> data, Vector2 assigned, GCOperator op) {
        Precheck(data, op);
        if (members.Count > 0) throw new Exception($"Can't assign V2 to V2 member {this}");
        if (op == GCOperator.Assign) return assigned;
        var v2 = data[var];
        ResolveFloat(ref v2.x, assigned.x, op);
        ResolveFloat(ref v2.y, assigned.y, op);
        return v2;
    }

    public Vector2 ResolveMembers(IReadOnlyDictionary<string, Vector2> data, float assigned, GCOperator op) {
        Precheck(data, op);
        if (members.Count == 0) throw new Exception($"Can't assign float to V2 {this}");
        var v2 = data[var];
        if (members[0] == "x") ResolveFloat(ref v2.x, assigned, op);
        else if (members[0] == "y") ResolveFloat(ref v2.y, assigned, op);
        else throw new Exception($"Can't get V2.f member {members[0]}");
        return v2;
    }

    public Vector3 ResolveMembers(IReadOnlyDictionary<string, Vector3> data, Vector3 assigned, GCOperator op) {
        Precheck(data, op);
        if (members.Count > 0) throw new Exception($"Can't assign V3 to V3 member {this}");
        if (op == GCOperator.Assign) return assigned;
        var v3 = data[var];
        ResolveFloat(ref v3.x, assigned.x, op);
        ResolveFloat(ref v3.y, assigned.y, op);
        ResolveFloat(ref v3.z, assigned.z, op);
        return v3;
    }

    public Vector3 ResolveMembers(IReadOnlyDictionary<string, Vector3> data, Vector2 assigned, GCOperator op) {
        Precheck(data, op);
        if (members.Count == 0) throw new Exception($"Can't assign V2 to V3 {this}");
        var v3 = data[var];
        if (members[0] == "xy") {
            ResolveFloat(ref v3.x, assigned.x, op);
            ResolveFloat(ref v3.y, assigned.y, op);
        } else if (members[0] == "yz") {
            ResolveFloat(ref v3.y, assigned.x, op);
            ResolveFloat(ref v3.z, assigned.y, op);
        } else if (members[0] == "xz") {
            ResolveFloat(ref v3.x, assigned.x, op);
            ResolveFloat(ref v3.z, assigned.y, op);
        } else throw new Exception($"Can't get V3.V2 member {members[0]}");
        return v3;
    }

    public Vector3 ResolveMembers(IReadOnlyDictionary<string, Vector3> data, float assigned, GCOperator op) {
        Precheck(data, op);
        if (members.Count == 0) throw new Exception($"Can't assign float to V3 {this}");
        var v3 = data[var];
        if (members[0] == "x") ResolveFloat(ref v3.x, assigned, op);
        else if (members[0] == "y") ResolveFloat(ref v3.y, assigned, op);
        else if (members[0] == "z") ResolveFloat(ref v3.z, assigned, op);
        else throw new Exception($"Can't get V3.f member {members[0]}");
        return v3;
    }

    public V2RV2 ResolveMembers(IReadOnlyDictionary<string, V2RV2> data, V2RV2 assigned, GCOperator op) {
        Precheck(data, op);
        if (members.Count > 0) throw new Exception($"Can't assign RV2 to RV2 member {this}");
        if (op == GCOperator.Assign) return assigned;
        MutV2RV2 rv2 = data[var];
        ResolveFloat(ref rv2.nx, assigned.nx, op);
        ResolveFloat(ref rv2.ny, assigned.ny, op);
        ResolveFloat(ref rv2.rx, assigned.rx, op);
        ResolveFloat(ref rv2.ry, assigned.ry, op);
        ResolveFloat(ref rv2.angle, assigned.angle, op);
        return rv2;
    }

    public V2RV2 ResolveMembers(IReadOnlyDictionary<string, V2RV2> data, float assigned, GCOperator op) {
        Precheck(data, op);
        if (members.Count == 0) throw new Exception($"Can't assign float to V2RV2 {this}");
        MutV2RV2 rv2 = data[var];
        if (members[0] == "nx") ResolveFloat(ref rv2.nx, assigned, op);
        else if (members[0] == "ny") ResolveFloat(ref rv2.ny, assigned, op);
        else if (members[0] == "rx") ResolveFloat(ref rv2.rx, assigned, op);
        else if (members[0] == "ry") ResolveFloat(ref rv2.ry, assigned, op);
        else if (members[0].StartsWith("a")) ResolveFloat(ref rv2.angle, assigned, op);
        else throw new Exception($"Can't get RV2.f member {members[0]}");
        return rv2;
    }

    public V2RV2 ResolveMembers(IReadOnlyDictionary<string, V2RV2> data, Vector2 assigned, GCOperator op) {
        Precheck(data, op);
        if (members.Count == 0) throw new Exception($"Can't assign V2 to V2RV2 {this}");
        MutV2RV2 rv2 = data[var];
        if (members[0] == "rxy") {
            ResolveFloat(ref rv2.rx, assigned.x, op);
            ResolveFloat(ref rv2.ry, assigned.y, op);
        } else if (members[0] == "nxy") {
            ResolveFloat(ref rv2.nx, assigned.x, op);
            ResolveFloat(ref rv2.ny, assigned.y, op);
        } else throw new Exception($"Can't get RV2.V2 member {members[0]}");
        return rv2;
    }

    public static implicit operator ReferenceMember(string s) => new ReferenceMember(s);
}

}
