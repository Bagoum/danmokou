using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Linq.Expressions;
using Danmokou.DMath;
using Danmokou.Expressions;
using JetBrains.Annotations;
using Ex = System.Linq.Expressions.Expression;
using PEx = System.Linq.Expressions.ParameterExpression;
using static Danmokou.DMath.Functions.ExM;
using static Danmokou.Expressions.ExMHelpers;
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace Danmokou.Expressions {
public class DerivativeException : Exception {
    public DerivativeException(string message) : base(message) { }
    public DerivativeException(string message, Exception inner) : base(message, inner) { }
}
class DerivativeVisitor : ExpressionVisitor {
    private static Ex ExC(object obj) => Ex.Constant(obj);

    private readonly Ex x;
    private readonly Ex dx;
    private DerivativeVisitor(Ex x, Ex dx) {
        this.x = x;
        this.dx = dx;
    }
    public static Ex Derivate(Ex x, Ex dx, Ex ex) => new DerivativeVisitor(x, dx).Visit(ex);

    private static readonly HashSet<Type> mathTypes = new HashSet<Type>() {
        typeof(Math),
        typeof(Mathf),
        typeof(M)
    };

    protected override Expression VisitBlock(BlockExpression node) {
        return Ex.Block(node.Variables, node.Expressions.Select(Visit));
    }

    public override Expression Visit(Expression node)  => (x == node) ? dx : base.Visit(node);

    private static readonly Dictionary<ExpressionType, Func<DerivativeVisitor, Ex, Ex, Ex>> BinOpReducers = 
        new Dictionary<ExpressionType, Func<DerivativeVisitor, Ex, Ex, Ex>>() {
        {ExpressionType.Add, (v,x,y) => v.Visit(x).Add(v.Visit(y))},
        {ExpressionType.Multiply, (v,x,y) => v.Visit(x).Mul(y).Add(x.Mul(v.Visit(y)))},
        {ExpressionType.Subtract, (v,x,y) => v.Visit(x).Sub(v.Visit(y))},
        {ExpressionType.Divide, (v,x,y) => v.Visit(x).Mul(y).Sub(x.Mul(v.Visit(y))).Div(y.Mul(y))},
    };
    
    private readonly Dictionary<ParameterExpression, Ex> derivMap = new Dictionary<ParameterExpression, Expression>();

    protected override Expression VisitParameter(ParameterExpression node) {
        return derivMap.TryGetValue(node, out var d) ? d : E0;
    }
    
    protected override Expression VisitMember(MemberExpression node) {
        return E0;
    }

    protected override Expression VisitBinary(BinaryExpression node) {
        if (BinOpReducers.TryGetValue(node.NodeType, out var f)) return f(this, node.Left, node.Right);
        if (node.NodeType == ExpressionType.Assign) {
            if (node.Left is ParameterExpression pex) {
                derivMap[pex] = Visit(node.Right);
                return node;
            }
        }
        throw new DerivativeException($"Couldn't resolve binary op {node.NodeType}");
    }

    protected override Expression VisitUnary(UnaryExpression node) {
        var v = Visit(node.Operand);
        if (node.NodeType == ExpressionType.Convert) {
            if (node.Type == v.Type) return v; //This may occur if DFD methods are derivatived
        }
        return base.VisitUnary(node);
    }

    protected override Expression VisitConstant(ConstantExpression node) => ExMHelpers.E0;

    protected override Expression VisitMethodCall(MethodCallExpression node) {
        var args = node.Arguments;
        if (mathTypes.Contains(node.Method.DeclaringType)) {
            var a1 = Visit(args[0]);
            if (node.Method.Name == "Sin") return a1.Mul(Cos(args[0]));
            if (node.Method.Name == "Cos") return a1.Neg().Mul(Sin(args[0]));
            if (node.Method.Name == "SinDeg") return a1.Mul(RadDeg(CosDeg(args[0])));
            if (node.Method.Name == "CosDeg") return a1.Neg().Mul(RadDeg(SinDeg(args[0])));
            if (node.Method.Name == "Pow" && args[1] is UnaryExpression exp) {
                //TODO a safer architecture for handling double casting
                if (!(Visit(exp.Operand).TryAsConst(out float f) && f == 0))
                    throw new DerivativeException("Power call has a non-constant exponent");
                else return exp.Operand.Mul(Pow(args[0], exp.Operand.Sub(E1)));
            }
            if (node.Method.Name == "Floor" || node.Method.Name == "Ceiling") return E0;
            if (node.Method.Name == "Min") return Ex.Condition(args[0].LT(args[1]), Visit(args[0]), Visit(args[1]));
            if (node.Method.Name == "Max") return Ex.Condition(args[0].GT(args[1]), Visit(args[0]), Visit(args[1]));
        }
        throw new DerivativeException($"Couldn't resolve method call {node.Method.Name}");
    }

    protected override Expression VisitConditional(ConditionalExpression node) {
        return Ex.Condition(node.Test, Visit(node.IfTrue), Visit(node.IfFalse), node.Type);
    }
}
}