using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Danmokou.Reflection;
using Danmokou.Reflection2;

namespace Danmokou.Expressions {

public class ReplaceExVisitor : ExpressionVisitor {
    private readonly Dictionary<Expression, Expression> map;
    public readonly Dictionary<object, Expression> constMap;
    private readonly Func<Dictionary<object, Expression>, object, Expression> fallbackConstHandling;
    
    public ReplaceExVisitor(Dictionary<Expression, Expression> map, Dictionary<object, Expression> objMap,
        Func<Dictionary<object, Expression>, object, Expression> fallbackConstHandling) {
        this.map = map;
        this.fallbackConstHandling = fallbackConstHandling;
        constMap = objMap;
    }

    public override Expression Visit(Expression? node) => 
        node != null && map.TryGetValue(node, out var repl) ? 
            repl : 
            base.Visit(node);

    protected override Expression VisitConstant(ConstantExpression node) {
        if (node.Value == null)
            return base.VisitConstant(node);
        if (constMap.TryGetValue(node.Value, out var repl))
            return repl;
        if (node.Value == ExMHelpers.LookupTable)
            return ExMHelpers.exLookupTable;
        return fallbackConstHandling(constMap, node.Value);
    }
}
}