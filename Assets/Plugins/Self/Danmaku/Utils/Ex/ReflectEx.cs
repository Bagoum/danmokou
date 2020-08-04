using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using UnityEngine;
using System;
using Danmaku;
using DMath;
using JetBrains.Annotations;
using Ex = System.Linq.Expressions.Expression;
using static ExUtils;
using ExGCXF = System.Func<DMath.TExGCX, TEx>;


public static class ReflectEx {

    private static readonly Dictionary<string, Stack<Expression>> alias_stack = 
        new Dictionary<string, Stack<Expression>>();
    [CanBeNull] private static TExGCX aliased_gcx = null;
    public static void SetGCX(TExGCX gcx) => aliased_gcx = gcx;
    public static void RemoveGCX() => aliased_gcx = null;

    public class LetDirect : IDisposable {
        private readonly string alias;
        public LetDirect(string alias, Ex val) {
            this.alias = alias;
            alias_stack.Push(alias, val);
        }
        
        public void Dispose() {
            alias_stack.Pop(alias);
        }
    }

    public static TEx<T> Let<WF, T, L>(string alias, Func<WF, TEx<L>> content, Func<TEx<T>> inner, WF applier) {
        var variabl = V<L>();
        alias_stack.Push(alias, variabl);
        var bl = Ex.Block(new[] {variabl},
            Ex.Assign(variabl, content(applier)),
            inner()
        );
        alias_stack.Pop(alias);
        return bl;
    }

    public readonly struct Alias<WF> {
        public readonly string alias;
        public readonly Func<WF, TEx> func;

        public Alias(string alias, Func<WF, TEx> func) {
            this.alias = alias;
            this.func = func;
        }
    }
    public static Ex Let2<WF>(Alias<WF>[] aliases, Func<Ex> inner, WF applier) {
        var stmts = new Ex[aliases.Length + 1];
        var vars = new ParameterExpression[aliases.Length];
        for (int ii = 0; ii < aliases.Length; ++ii) {
            Ex alias_value = aliases[ii].func(applier);
            alias_stack.Push(aliases[ii].alias, vars[ii] = V(alias_value.Type));
            stmts[ii] = Ex.Assign(vars[ii], alias_value);
        }
        stmts[aliases.Length] = inner();
        var bl = Ex.Block(vars, stmts);
        for (int ii = 0; ii < aliases.Length; ++ii) {
            alias_stack.Pop(aliases[ii].alias);
        }
        return bl;
    }
    public static TEx<T> Let<WF, T, L>((string alias, Func<WF, TEx<L>> content)[] aliases, Func<TEx<T>> inner,
        WF applier) {
        var stmts = new Ex[aliases.Length + 1];
        var vars = new ParameterExpression[aliases.Length];
        for (int ii = 0; ii < aliases.Length; ++ii) {
            Ex alias_value = aliases[ii].content(applier);
            alias_stack.Push(aliases[ii].alias, vars[ii] = V(alias_value.Type));
            stmts[ii] = Ex.Assign(vars[ii], alias_value);
        }
        stmts[aliases.Length] = inner();
        var bl = Ex.Block(vars, stmts);
        for (int ii = 0; ii < aliases.Length; ++ii) {
            alias_stack.Pop(aliases[ii].alias);
        }
        return bl;
    }
/*
    private static TEx<T> LetLambda<WF, T, L>((string alias, string[] args, Func<WF, TEx<L>> content)[] lambdas,
        Func<TEx<T>> inner, Dictionary<string, (string[], Func<WF, TEx<L>>)> stack) {
        
    }*/

    public interface ICompileReferenceResolver {
        bool TryResolve<T>(string alias, out Ex ex);
    }
    
    [CanBeNull] private static ICompileReferenceResolver icrr = null;
    public static void SetICRR(ICompileReferenceResolver icr) => icrr = icr;
    public static void RemoveICRR() => icrr = null;
    
    public static Ex ReferenceExpr<T>(string alias, [CanBeNull] TExPI bpi) {
        if (alias[0] == Parser.SM_REF_KEY_C) alias = alias.Substring(1); //Important for reflector handling of &x
        //Standard method, used by ::{} and GCXPath.expose
        if (alias_stack.TryGetValue(alias, out var f)) return f.Peek();
        //Used by GCXF<T>, ie. when a fixed GCX exists. This is for slower pattern expressions
        if (aliased_gcx != null) return aliased_gcx.FindReference<T>(alias);
        //Automatic GCXPath.expose resolution
        if (icrr != null && icrr.TryResolve<T>(alias, out Ex ex)) return ex;
        //In functions not scoped by the GCX (eg. bullet controls)
        //The reason for using the special marker is that we cannot give good errors if an incorrect value is entered
        //(good error handling would make lookup slower, and this is hotpath),
        //so we need to make opting into this completely explicit. 
        if (alias.StartsWith(".") && bpi != null) {
            string halias = alias.Substring(1);
            return PrivateDataHoisting.GetValue(bpi, Reflector.AsExType<T>(), halias);
        }
        throw new Exception($"The reference {alias} is used, but does not have a value.");
    }
    public static Func<TExPI, TEx<T>> ReferenceLetBPI<T>(string alias) => bpi => ReferenceExpr<T>(alias, bpi);
    public static Func<WF, TEx<T>> ReferenceLet<WF, T>(string alias) => t => ReferenceExpr<T>(alias, null);
    public static Func<TEx<T>> ReferenceLet<T>(string alias) => () => ReferenceExpr<T>(alias, null);

    public readonly struct Hoist<T> {
        private readonly SafeResizableArray<T> data;

        public Hoist(string name) {
            data = PublicDataHoisting.Register<T>(name);
        }
        public void Save(int index, T value) => data.SafeAssign(index, value);
        public T Retrieve(int index) => data.SafeGet(index);

        public Ex Save(Ex index, Ex val) => data.SafeAssign(Ex.Convert(index, tint), val);
        public Ex Retrieve(Ex index) => data.SafeGet(Ex.Convert(index, tint));
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
        if (!data.ContainsKey(var) && members.Count > 0) throw new Exception($"Can't write members when value does not exist: {this}");
        if (!data.ContainsKey(var) && op != GCOperator.Assign) throw new Exception($"New variable {this} can only be assigned, but was given operator {op}");
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

    public static implicit operator ReferenceMember(string s) => new ReferenceMember(s);
}

