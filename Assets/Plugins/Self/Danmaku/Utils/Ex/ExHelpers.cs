using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Linq.Expressions;
using JetBrains.Annotations;
using Ex = System.Linq.Expressions.Expression;
using PEx = System.Linq.Expressions.ParameterExpression;

namespace DMath {

public static class ExHelpers {
    public static Func<float, float, Vector2> XYV2(PEx x, PEx y, PEx v2, params Ex[] actions) =>
        Ex.Lambda<Func<float, float, Vector2>>(Ex.Block(new[] {v2},
                actions.Append(v2)), x, y).Compile();

}

}