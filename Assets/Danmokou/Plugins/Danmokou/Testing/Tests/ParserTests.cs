using System;
using Danmokou.Core;
using Danmokou.Services;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.Reflection;
using NUnit.Framework;
using UnityEngine;
using static NUnit.Framework.Assert;
using static Danmokou.Testing.TAssert;

namespace Danmokou.Testing {

public static class ParserTests {
    private const float err = 0.00001f;

    [Test]
    public static void TestArray() {
        AreEqual("{ 8 + 2 4 }".Into<FXY[]>()[1](0), 6);
    }
    [Test]
    public static void CommasWork() {
        //this one is difficult...
        AreEqual("+ (x) 3".Into<FXY>()(2), 5);
        AreEqual("+ x (3)".Into<FXY>()(2), 5);
        AreEqual("(+ 8 *(5, x))".Into<FXY>()(2), 18);
        AreEqual("(+ 8 *(5, x))".Into<FXY>()(2), 18);
        AreEqual("+ x() 3".Into<FXY>()(2), 5);
        AreEqual("+ 8 (if = p 0 0 6)".Into<BPY>()(new ParametricInfo(Vector2.down, 5, 0)), 14);
        AreEqual("+(x, 2)".Into<FXY>()(2), 4);
        AreEqual("+((c x), 2)".Into<FXY>()(2), 1);
        AreEqual("(((((+(x, 2))))))".Into<FXY>()(2), 4);
        AreEqual("+(x, (2))".Into<FXY>()(2), 4);
        AreEqual("+(x, * x 2)".Into<FXY>()(2), 6);
        AreEqual("+(x, *(x, 2))".Into<FXY>()(2), 6);
        AreEqual("+(x, *((x), ((2))))".Into<FXY>()(2), 6);
        // (f x) y
        AreEqual("*(+ 3 5)(x)".Into<FXY>()(2), 16);
    }

    [Test]
    public static void Errors() {
        ThrowsMessage("do not enclose the entire function", () => "+ 8 (if = p 0) 0 6".Into<BPY>());
        ThrowsMessage("must have exactly one argument", () => "(+ 8 * 5, x)".Into<FXY>());
        ThrowsMessage("trying to create an object of type BPY", () => "+(x, *(x 2))".Into<FXY>());
        
    }

    [Test]
    public static void GroupingErrors() {
        ThrowsMessage("trying to create an object of type BPY", () => "+(x, (2)())".Into<FXY>());
        ThrowsMessage("trying to create an object of type BPY", () => "modwithpause 5 (6 7) 8".Into<BPY>());
        ThrowsMessage("Expected 4 explicit arguments.*contains 3", () => "modwithpause(5, (6 7), 8)".Into<BPY>());
        ThrowsMessage("could not parse the second", () => "mod(3 *, 5)".Into<FXY>());
        ThrowsMessage("trying to create an object of type BPY", () => "+(2 * 5 x)".Into<FXY>());
    }
    
    private static Vector2 V2(float x, float y) => new Vector2(x, y);

    private const string preAggSecond = "";
    //[Test]
    public static void PreAggregation() {
        var bpi = new ParametricInfo {t = 6};
        AreEqual("(t + 1) * cxy 2 3".Into<TP>()(bpi), V2(14, 21));
        ThrowsMessage("first creating type BPY", () => "mod(px(3), 5) * cx(2)".Into<TP>());
        AreEqual("(t * cxy 2 3)".Into<TP>()(bpi), V2(12, 18));
        AreEqual("t * cxy 2 3".Into<TP>()(bpi), V2(12, 18));
        AreEqual("pxyz(0,0,0)".Into<TP>()(bpi), V2(0, 0));
        //Note that this is grouped as t * (2 * cxy 2 3)
        AreEqual("t * 2 * cxy 2 3".Into<TP>()(bpi), V2(24, 36));
        //ThrowsMessage("first creating type BPY", () => "mod(3, 5) * 7".Into<TP>());
        //I think you need checkpointing for this to work.
        //"+(3, 5) * cx(2)".Into<TP>();
        "rotatev(t * 5, px(2))".Into<TP>();
    }

    [Test]
    public static void PostAggregationErrors() {
        
    }

    [Test]
    public static void tmp() {
        GameManagement.NewInstance(InstanceMode.NULL);
        AreEqual("(-1 + x + 2 * y + 3)".Into<BPY>()(new ParametricInfo() { loc = new Vector2(5, 10)}), 27);
        "(/ 5 * ^ dl 0.8 24)".Into<BPY>();
        AreEqual(@"
!!{ jt 2
!!{ movet 1.5
($movet + $jt - 1.5)".Into<FXY>()(65), 2f);
    }
    [Test]
    public static void PostAggregation() {
        AreEqual("((2) + 3)".Into<FXY>()(0), 5);
        AreEqual("(((2) + 3) * 2)".Into<FXY>()(0), 10);
        AreEqual("+(2 * 5, x)".Into<FXY>()(4), 14);
        AreEqual("(3 * 2 + 2)".Into<FXY>()(0), 8);
        AreEqual("(3 + x)".Into<FXY>()(7), 10);
        AreEqual("(+ 2 * 5 x)".Into<FXY>()(4), 22);
        //Works at top level!
        AreEqual("3 + x".Into<FXY>()(7), 10);
        AreEqual("(5 * 3 / 2)".Into<FXY>()(0), 7.5f);
        AreEqual("(5 * 3 // 6)".Into<FXY>()(0), 2f);
        "(3 * 2 + 2)".Into<Func<TExArgCtx, TEx<float>>>();
        AreEqual("((3 * 2 + 2))".Into<FXY>()(0), 8);
        AreEqual("((2 + 3) * 4)".Into<FXY>()(0), 20);
        AreEqual("(3 * (2 + 2))".Into<FXY>()(0), 12);
        AreEqual("(3 * 2 + 2)".Into<FXY>()(0), 8);
        AreEqual("(2 + 3 * 4)".Into<FXY>()(0), 14);
        AreEqual("(2 + (3 * 4))".Into<FXY>()(0), 14);
        "Mod(2 + Mod(x + 2, x + 4), 5 + +(2, 3))".Into<BPY>();
        "Mod(x + 2, x + 4)".Into<BPY>();
        AreEqual("if(> t 5, 10 + 2, 3 + 4)".Into<BPY>()(new ParametricInfo(){t = 6}), 12);
        AreEqual("if(> t 5) (10 + 2) (3 + 4)".Into<BPY>()(new ParametricInfo(){t = 6}), 12);
    }
}
}