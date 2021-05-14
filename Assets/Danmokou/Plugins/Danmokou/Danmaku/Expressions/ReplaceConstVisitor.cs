using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Danmokou.Expressions {

public class ReplaceExVisitor : ExpressionVisitor {
    private readonly Dictionary<Expression, Expression> map;
    private readonly Dictionary<object, Expression> constMap;
    private readonly Func<Dictionary<object, Expression>, object, Expression> fallbackConstHandling;
    
    public ReplaceExVisitor(Dictionary<Expression, Expression> map, Dictionary<object, Expression> defaultObjMap,
        Func<Dictionary<object, Expression>, object, Expression> fallbackConstHandling) {
        this.map = map;
        this.fallbackConstHandling = fallbackConstHandling;
        constMap = new Dictionary<object, Expression>();
        foreach (var key in defaultObjMap.Keys) {
            constMap[key] = defaultObjMap[key];
        }
        foreach (var key in map.Keys) {
            if (key is ConstantExpression cx) {
                constMap[cx.Value] = map[key];
            }
        }
    }

    public override Expression Visit(Expression? node) => 
        node != null && map.TryGetValue(node, out var repl) ? 
            repl : 
            base.Visit(node);

    protected override Expression VisitConstant(ConstantExpression node) {
        if (node.Value == null)
            return base.VisitConstant(node);
        return constMap.TryGetValue(node.Value, out var repl) ? 
            repl : 
            fallbackConstHandling(constMap, node.Value);
    }
}
}