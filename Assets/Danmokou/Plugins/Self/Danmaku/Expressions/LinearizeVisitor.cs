using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using DMK.Core;
using JetBrains.Annotations;
using Ex = System.Linq.Expressions.Expression;

namespace DMK.Expressions {

/// <summary>
/// Linearizes BlockExpressions by (for BlockExpressions) removing any nesting, and (for other expressions)
///  removing BlockExpression children if they are not allowed in C# syntax proper.
/// Eg. Assign cannot have BlockExpression children, but If can.
/// </summary>
public class LinearizeVisitor : ExpressionVisitor {

    private int counter = 0;

    private ParameterExpression Variable(Type t) => Ex.Variable(t, $"linz_{counter++}");

    /// <summary>
    /// Invariant: bex is linearized.
    /// </summary>
    private BlockExpression AssignBlockResultToTemp(BlockExpression bex, ParameterExpression pex) {
        var exprs = bex.Expressions.ToArray();
        exprs[exprs.Length - 1] = Ex.Assign(pex, exprs[exprs.Length - 1]);
        return Ex.Block(bex.Variables, exprs);
    }

    protected override Expression VisitBlock(BlockExpression node) {
        if (node.Expressions.Count == 1 && node.Variables.Count == 0) return Visit(node.Expressions[0]);
        var prms = new List<ParameterExpression>();
        var stmts = new List<Expression>();
        prms.AddRange(node.Variables);
        foreach (var ex in node.Expressions) {
            var linearized = Visit(ex);
            if (linearized is BlockExpression bex) {
                prms.AddRange(bex.Variables);
                stmts.AddRange(bex.Expressions);
            } else {
                stmts.Add(linearized);
            }
        }
        return Ex.Block(prms, stmts);
    }

    /// <summary>
    /// For each expression provided, linearize it if necessary, and then linearize the process of combining
    /// the expressions by assigning all block results to temporary variables and then using those temporary
    /// variables instead of the blocks.
    /// </summary>
    private Expression Linearize(Func<Expression[], Expression> combiner, params Expression[] pieces) {
        var linearized = pieces.Select(Visit).ToArray();
        //Best case!
        if (!linearized.Any(ex => ex is BlockExpression))
            return combiner(linearized);
        var prms = new List<ParameterExpression>();
        var stmts = new List<Expression>();
        var reduced_args = new Expression[linearized.Length];
        linearized.ForEachI((i, ex) => {
            if (ex is BlockExpression bex) {
                prms.AddRange(bex.Variables);
                stmts.AddRange(bex.Expressions.Take(bex.Expressions.Count - 1));
                reduced_args[i] = bex.Expressions[bex.Expressions.Count - 1];
            } else {
                reduced_args[i] = ex;
            }
        });
        stmts.Add(combiner(reduced_args));
        return Ex.Block(prms, stmts);
    }

    protected override Expression VisitUnary(UnaryExpression node) =>
        Linearize(exs => Ex.MakeUnary(node.NodeType, exs[0], node.Type), node.Operand);

    protected override Expression VisitBinary(BinaryExpression node) =>
        Linearize(exs => Ex.MakeBinary(node.NodeType, exs[0], exs[1]), node.Left, node.Right);

    protected override Expression VisitConditional(ConditionalExpression node) {
        if (node.Type == typeof(void)) {
            //If/then statements only require fixing the condition, since if statements can take blocks as children
            return Linearize(cond => Ex.Condition(cond[0], Visit(node.IfTrue), Visit(node.IfFalse), node.Type), node.Test);
        } else {
            var ifT = Visit(node.IfTrue);
            var ifF = Visit(node.IfFalse);
            Ex WithWrite(Ex branch, ParameterExpression dst) {
                if (branch is BlockExpression bex) {
                    return AssignBlockResultToTemp(bex, dst);
                } else {
                    return Ex.Block(dst.Is(branch));
                }
            }
            if (ifF is BlockExpression || ifT is BlockExpression) {
                //This handling is more complex than the standard handling since it'd be incorrect
                // to just evaluate both branches and return the correct one.
                //Instead, we declare a variable outside an if statement, and write to it in the branches.
                var prm = Variable(node.Type);
                //Since we create a nested block here we need to re-linearize it as well...
                return VisitBlock(Ex.Block(new[] {prm},
                    Linearize(cond => Ex.Condition(cond[0], WithWrite(ifT, prm), WithWrite(ifF, prm), typeof(void)), node.Test),
                    prm
                ));
            } else
                return Linearize(cond => Ex.Condition(cond[0], ifT, ifF, node.Type), node.Test);
        }
    }

    protected override Expression VisitNew(NewExpression node) =>
        Linearize(args => Ex.New(node.Constructor, args), node.Arguments.ToArray());

    protected override Expression VisitMethodCall(MethodCallExpression node) =>
        Linearize(args => Ex.Call(node.Object, node.Method, args), node.Arguments.ToArray());

    protected override Expression VisitIndex(IndexExpression node) =>
        Linearize(args => Ex.MakeIndex(args[0], node.Indexer, args.Skip(1)), 
            node.Arguments.Prepend(node.Object).ToArray());

    protected override Expression VisitMember(MemberExpression node) =>
        Linearize(args => Ex.MakeMemberAccess(args[0], node.Member), node.Expression);
}
}