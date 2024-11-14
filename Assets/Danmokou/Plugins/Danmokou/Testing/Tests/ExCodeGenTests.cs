using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Danmokou.Core;
using Danmokou.Services;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Patterns;
using Danmokou.DataHoist;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Expressions;
using Danmokou.GameInstance;
using Danmokou.Reflection;
using Danmokou.SM;
using NUnit.Framework;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using ExTP = System.Func<Scriptor.Expressions.TExArgCtx, Scriptor.Expressions.TEx<UnityEngine.Vector2>>;

namespace Danmokou.Testing {

public class ExCodeGenTests {
    

    [Test]
    public void TestPrivateHoist() {
        GameManagement.NewInstance(InstanceMode.NULL, InstanceFeatures.InactiveFeatures);
        var ex = "sine(13h, 0.4, 13 * pi + p / dl ^ 1.2)".Into<GCXF<float>>();
        var ex2 = @"sync ""sun-red/w"" <2;:> gsr {
	start b{ 
        hvar gcxVar = pxy(1, 2)
    }
} s offset b{
    var letVar1 = 0
	var letVar2 = (-1 + letVar1)
    px(letVar2)
} b{
	var letVar1 = gcxVar.x
	var letVar2 = (1 + letVar1)
    pxy load0(""publicVar"") ss0(letVar2)
}".Into<StateMachine>();
        //var s = BakeCodeGenerator.Generated;

    }

    
    public void TestCosSin() {
        var dct = new Dictionary<int, int>();
        var ex = Ex.Constant(dct);

        var block = Ex.Block(new ParameterExpression[0],
            Ex.Call(ex, "Clear", new Type[0]),
            Ex.Constant(true)
        );
        var block2 = Ex.Block(new ParameterExpression[0],
            Ex.Call(ex, "Clear", new Type[0])
        );

        var ifex = Ex.Block(new ParameterExpression[0],
            Ex.IfThenElse(block, block2, block2),
            Ex.Constant(2f));
        //if: only the condition needs to be linearized
        //cond: condition, true, false need to be linarized
        
        //the library is kinda broken in that if, if/else statements will always print return statements
        //if the last statement is not a void type...
        //in other words it doesn't dewal with blocks well
        

    }
    

    
}
}