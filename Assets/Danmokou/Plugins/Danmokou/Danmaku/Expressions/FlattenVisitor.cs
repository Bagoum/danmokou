using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using BagoumLib;
using BagoumLib.Expressions;
using Danmokou.Core;
using Danmokou.DMath;
using Ex = System.Linq.Expressions.Expression;
using PEx = System.Linq.Expressions.ParameterExpression;
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace Danmokou.Expressions {
class DeactivateConstantVisitor : ExpressionVisitor {
    private readonly Dictionary<ParameterExpression, Ex> ConstValPrmsMap;
    public DeactivateConstantVisitor(Dictionary<ParameterExpression, Ex> prmMap) {
        ConstValPrmsMap = prmMap;
    }
    protected override Expression VisitParameter(ParameterExpression node) {
        ConstValPrmsMap.Remove(node);
        return node;
    }
}
class FlattenVisitor : ExpressionVisitor {
    private static Ex ExC(object obj) => Ex.Constant(obj);
    private static readonly Dictionary<ExpressionType, Func<float, float, object>> BinOpReducers = new Dictionary<ExpressionType, Func<float, float, object>>() {
        {ExpressionType.Add, (x,y) => x + y},
        {ExpressionType.Multiply, (x,y) => x * y},
        {ExpressionType.Subtract, (x,y) => x - y},
        {ExpressionType.Divide, (x,y) => x / y},
        {ExpressionType.GreaterThan, (x,y) => x > y},
        {ExpressionType.LessThan, (x,y) => x < y},
        {ExpressionType.Equal, (x,y) => x == y},
    };

    private readonly Dictionary<ParameterExpression, Ex> ConstValPrmsMap =
        new Dictionary<ParameterExpression, Ex>();
    private readonly Stack<int> VariableReductionAllowed = new Stack<int>();
    
    private static readonly HashSet<ExpressionType> AssignTypes = new HashSet<ExpressionType>() {
        ExpressionType.Assign, ExpressionType.AddAssign, ExpressionType.AndAssign, ExpressionType.AddAssignChecked,
        ExpressionType.DivideAssign, ExpressionType.ModuloAssign, ExpressionType.MultiplyAssign, ExpressionType.OrAssign,
        ExpressionType.PowerAssign, ExpressionType.PowerAssign, ExpressionType.SubtractAssign, ExpressionType.SubtractAssignChecked,
        ExpressionType.LeftShiftAssign, ExpressionType.ExclusiveOrAssign, ExpressionType.MultiplyAssignChecked, 
        ExpressionType.PostDecrementAssign, ExpressionType.PreDecrementAssign, ExpressionType.RightShiftAssign
    };

    private readonly bool reduceMethod;
    private readonly bool reduceField;
    public FlattenVisitor(bool reduceMethod, bool reduceField) {
        this.reduceMethod = reduceMethod;
        this.reduceField = reduceField;
    }
    public static Ex Flatten(Ex ex, bool reduceMethod = true, bool reduceField = true) {
        var fv = new FlattenVisitor(reduceMethod, reduceField);
        ex = fv.Visit(ex);
        return ex;
    }

    protected override Expression VisitBinary(BinaryExpression node) {
        var l = AssignTypes.Contains(node.NodeType) ? node.Left : Visit(node.Left);
        var r = Visit(node.Right);
        bool LeftIs(Func<float, bool> cond, Func<Ex, Ex> ret, out Ex early) {
            if (l.TryAsConst(out float fl) && cond(fl)) {
                early = ret(r);
                return true;
            }
            early = null!;
            return false;
        }
        bool RightIs(Func<float, bool> cond, Func<Ex, Ex> ret, out Ex early) {
            if (r.TryAsConst(out float fr) && cond(fr)) {
                early = ret(l);
                return true;
            }
            early = null!;
            return false;
        }
        bool Bifocal(Func<float,bool> cond, Func<Ex, Ex> ret, out Ex early) {
            return LeftIs(cond, ret, out early) || RightIs(cond, ret, out early);
        }
        if (l.TryAsConst(out float vl) && r.TryAsConst(out float vr) &&
            BinOpReducers.TryGetValue(node.NodeType, out var reducer)) {
            return ExC(reducer(vl, vr));
        }
        if (node.NodeType == ExpressionType.Add && Bifocal(x => x == 0f, e => e, out var ex)) return ex;
        if (node.NodeType == ExpressionType.Multiply) {
            if (Bifocal(x => x == 0f, e => ExC(Activator.CreateInstance(e.Type)), out ex)) return ex;
            if (Bifocal(x => x == 1f, e => e, out ex)) return ex;
        }
        if (node.NodeType == ExpressionType.Subtract && RightIs(x => x == 0f, e => e, out ex)) return ex;
        if (node.NodeType == ExpressionType.Divide) {
            if (LeftIs(x => x == 0f, e => ExC(Activator.CreateInstance(e.Type)), out ex)) return ex;
            if (RightIs(x => x == 1f, e => e, out ex)) return ex;
        }
        //Any parameter on the left-hand side is no longer constant
        if (AssignTypes.Contains(node.NodeType)) {
            new DeactivateConstantVisitor(ConstValPrmsMap).Visit(l);
            if (node.NodeType == ExpressionType.Assign && l is ParameterExpression pex && 
                r.IsSimplifiable() && VariableReductionAllowed.Count == 0) {
                ConstValPrmsMap[pex] = r;
                //You can't return empty here. Consider this case:
                //y = 5
                //if (y > 3) { y = 2 } { z = 3 }
                //return y + 1
                //The assignment still needs to occur, allowing for this sort of setup
            }
        }
        return Expression.MakeBinary(node.NodeType, l, r);
    }

    private Expression DeferWithNoReduction<T>(Func<T, Expression> inner, T node) {
        VariableReductionAllowed.Push(0);
        var ret = inner(node);
        VariableReductionAllowed.Pop();
        return ret;
    }

    protected override Expression VisitConditional(ConditionalExpression node) {
        var test = Visit(node.Test);
        if (test.TryAsConst(out bool v)) {
            var result = Visit(v ? node.IfTrue : node.IfFalse);
            if (node.Type == typeof(void)) {
                //Don't change the return type
                return Ex.Block(result, Ex.Empty());
            } else
                return result;
        }
        return Expression.Condition(test, DeferWithNoReduction(base.Visit, node.IfTrue),
            DeferWithNoReduction(base.Visit, node.IfFalse), node.Type);
    }

    protected override Expression VisitLoop(LoopExpression node) => DeferWithNoReduction(base.VisitLoop, node);

    protected override Expression VisitTry(TryExpression node) => DeferWithNoReduction(base.VisitTry, node);

    //WARNING: (UNSAFE?) CONVERSIONS: DOUBLE CONVERSIONS CRUSHED

    //Double casts can be crushed if the outer cast is *less informative* than the inner cast (or they are the same).
    // Eg. (float)(int)5.3 cannot be crushed because float is more informative than int. It should return 5.
    // Eg. (int)(float)5.3 can be crushed because int is less informative than float. It should return 5.
    private static readonly HashSet<(Type outer, Type inner)> allowedDoubleCasts = new HashSet<(Type, Type)>() {
        (typeof(int), typeof(float)),
        (typeof(float), typeof(double)),
        (typeof(int), typeof(double))
    };
    
    protected override Expression VisitUnary(UnaryExpression node) {
        var o = Visit(node.Operand);
        if (node.NodeType == ExpressionType.Convert) {
            //Null typecasts don't work correctly with nullable types
#if !EXBAKE_SAVE && !EXBAKE_LOAD
            if (o.TryAsAnyConst(out var notNull) && notNull != null) {
                //Nested compile causes issues with baking
                return ExC(Ex.Lambda(Ex.Convert(o, node.Type)).Compile().DynamicInvoke());
            }
#endif
            if (o is UnaryExpression ue && ue.NodeType == ExpressionType.Convert) {
                if (node.Type == ue.Type || allowedDoubleCasts.Contains((node.Type, ue.Type)))
                    o = ue.Operand;
            }
            if (node.Type == o.Type) return o;
        } else if (node.NodeType == ExpressionType.Negate) {
            if (o.TryAsConst(out float f)) return ExC(-1f * f);
            if (o.NodeType == ExpressionType.Negate && o is UnaryExpression ue) return ue.Operand;
        }
        return Ex.MakeUnary(node.NodeType, o, node.Type);
    }

    private static readonly HashSet<Type> SafeNewReduceTypes = new HashSet<Type>() {
        typeof(Vector2),
        typeof(Vector3),
        typeof(Vector4),
        typeof(CCircle),
        typeof(CRect),
    };
    protected override Expression VisitNew(NewExpression node) {
        var newArgs = new object?[node.Arguments.Count];
        var visited = new Ex[node.Arguments.Count];
        bool isAllConst = SafeNewReduceTypes.Contains(node.Type);
        for (int ii = 0; ii < node.Arguments.Count; ++ii) {
            visited[ii] = Visit(node.Arguments[ii]);
            isAllConst &= visited[ii].TryAsAnyConst(out newArgs[ii]);
        }
        return isAllConst ?
            (Expression) Ex.Constant(node.Constructor.Invoke(newArgs)) :
            Ex.New(node.Constructor, visited);
    }

    private static readonly HashSet<Type> SafeRepoTypes = new HashSet<Type>() {
        typeof(Math),
        typeof(Mathf),
        typeof(M)
    };
    private static readonly Type dmkMathClassType = typeof(M);
    private static readonly Type systemMathType = typeof(Math);

    protected override Expression VisitMethodCall(MethodCallExpression node) {
        var newArgs = new object?[node.Arguments.Count];
        var visited = new Ex[node.Arguments.Count];
        bool isAllConst = node.Object == null && SafeRepoTypes.Contains(node.Method.DeclaringType); //only static methods
        for (int ii = 0; ii < node.Arguments.Count; ++ii) {
            visited[ii] = Visit(node.Arguments[ii]);
            isAllConst &= visited[ii].TryAsAnyConst(out newArgs[ii]);
        }
        if (isAllConst) return Ex.Constant(node.Method.Invoke(null, newArgs));
        if (node.Method.DeclaringType == dmkMathClassType) {
            if (reduceMethod) {
                //DMK math functions take float, but the converted versions take double, so we need to convert
                var v = Visit(visited[0].As<double>());
                if (node.Method.Name == "Sin")
                    return ExMHelpers.dLookupSinRad(v);
                if (node.Method.Name == "Cos")
                    return ExMHelpers.dLookupCosRad(v);
                if (node.Method.Name == "CosSin")
                    return ExMHelpers.dLookupCosSinRad(v);
                if (node.Method.Name == "SinDeg")
                    return ExMHelpers.dLookupSinDeg(v);
                if (node.Method.Name == "CosDeg")
                    return ExMHelpers.dLookupCosDeg(v);
                if (node.Method.Name == "CosSinDeg")
                    return ExMHelpers.dLookupCosSinDeg(v);
            }
        }else if (node.Method.DeclaringType == systemMathType) {
            if (node.Method.Name == "Pow" && visited[1].TryAsConst(out double d)) {
                if (d == 0)
                    return Ex.Constant(1.0, typeof(double));
                if (d == 1)
                    return visited[0];
                if (d == 2 && !EEx.RequiresCopyOnRepeat(visited[0]))
                    return visited[0].Mul(visited[0]);
            }
        }
        return Ex.Call(node.Object, node.Method, visited);
    }

    protected override Expression VisitBlock(BlockExpression node) {
        if (node.Expressions.Count == 1 && node.Variables.Count == 0) return Visit(node.Expressions[0]);
        return Ex.Block(node.Variables, node.Expressions.Select(Visit));
    }

    protected override Expression VisitParameter(ParameterExpression node) {
        if (ConstValPrmsMap.TryGetValue(node, out var v)) return v;
        return node;
    }

    protected override Expression VisitMember(MemberExpression node) {
        var cont = Visit(node.Expression);
        if (reduceField && cont.TryAsAnyConst(out var obj)) {
            //Do not reduce properties, only fields.
            if (node.Member is FieldInfo fi) return ExC(fi.GetValue(obj));
        }
        return Ex.MakeMemberAccess(cont, node.Member);
    }
}

class DebugVisitor : ExpressionVisitor {
    private static StringBuilder acc = null!;

    public string Export(Expression ex) {
        acc = new StringBuilder();
        Visit(ex);
        return acc.ToString();
    }
    protected override Expression VisitConstant(ConstantExpression node) {
        acc.Append(node.Value);
        return node;
    }

    private static readonly Dictionary<ExpressionType, string> BinaryOpMap = new Dictionary<ExpressionType, string>() {
        {ExpressionType.Add, "+"},
        {ExpressionType.Multiply, "*"},
        {ExpressionType.Subtract, "-"},
        {ExpressionType.Divide, "/"},
        {ExpressionType.Assign, "="},
        {ExpressionType.GreaterThan, ">"}
    };
    protected override Expression VisitBinary(BinaryExpression node) {
        acc.Append("(");
        Visit(node.Left);
        if (BinaryOpMap.TryGetValue(node.NodeType, out var s)) acc.Append(s);
        Visit(node.Right);
        acc.Append(")");
        return node;
    }

    protected override Expression VisitBlock(BlockExpression node) {
        acc.Append("(");
        node.Expressions.ForEachI((i, x) => {
            if (i > 0) acc.Append("\n");
            Visit(x);
            acc.Append(";");
        });
        acc.Append(")");
        return node;
    }

    protected override Expression VisitParameter(ParameterExpression node) {
        acc.Append(node.Name);
        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node) {
        acc.Append($"{node.Method.DeclaringType?.Name ?? "Null"}.{node.Method.Name}({node.Object?.Type.Name ?? "Null"}");
        foreach (var x in node.Arguments) {
            acc.Append(",");
            Visit(x);
        }
        acc.Append(")");
        return node;
    }

    protected override Expression VisitUnary(UnaryExpression node) {
        if (node.NodeType == ExpressionType.Convert) {
            Visit(node.Operand);
            acc.Append($":>{node.Type.Name}");
            return node;
        } else if (node.NodeType == ExpressionType.Negate) {
            acc.Append("-");
            Visit(node.Operand);
            return node;
        }
        return base.VisitUnary(node);
    }

    protected override Expression VisitConditional(ConditionalExpression node) {
        acc.Append("if");
        Visit(node.Test);
        acc.Append("{");
        Visit(node.IfTrue);
        acc.Append("}else{");
        Visit(node.IfFalse);
        acc.Append("}");
        return node;
    }

    protected override Expression VisitMember(MemberExpression node) {
        Visit(node.Expression);
        acc.Append($".{node.Member.Name}");
        return node;
    }
}


}