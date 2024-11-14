using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Danmokou.Reflection;

namespace Danmokou.Expressions {

public class ReplaceExVisitor : ExpressionVisitor {
    private readonly Dictionary<Expression, Expression> map;
    
    public ReplaceExVisitor(Dictionary<Expression, Expression> map) {
        this.map = map;
    }

    public override Expression Visit(Expression? node) => 
        node != null && map.TryGetValue(node, out var repl) ? 
            repl : 
            base.Visit(node);

    protected override Expression VisitConstant(ConstantExpression node) {
        if (node.Value == null)
            return base.VisitConstant(node);
        if (node.Value == DMKExMHelpers.LookupTable)
            return DMKExMHelpers.exLookupTable;
        return node;
    }
}
}