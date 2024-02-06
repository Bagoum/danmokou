using System;
using Danmokou.Core;
using Danmokou.Services;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.GameInstance;
using Danmokou.Reflection;
using Danmokou.SM;
using NUnit.Framework;
using UnityEngine;
using static NUnit.Framework.Assert;
using static Danmokou.Testing.TAssert;

namespace Danmokou.Testing {

public static class ParserTests {
    private const float err = 0.00001f;

    [Test]
    public static void TestArray() {
        AreEqual("{ 8.0, 2 + 4 }".Into<float[]>()[1], 6);
    }
    [Test]
    public static void CommasWork() {
        //this one is difficult...
        AreEqual("addTest (x) 3".Into<FXY>()(2), 5);
        AreEqual("addTest x (3)".Into<FXY>()(2), 5);
        AreEqual("(addTest 8 mulTest(5, x))".Into<FXY>()(2), 18);
        AreEqual("(addTest 8 mulTest(5, x))".Into<FXY>()(2), 18);
        AreEqual("addTest x() 3".Into<FXY>()(2), 5);
        AreEqual("addTest 8 (p == 0 ? 0 : 6)".Into<BPY>()(new ParametricInfo(Vector2.down, 5, 0)), 14);
        AreEqual("addTest(x, 2)".Into<FXY>()(2), 4);
        AreEqual("addTest((c x), 2)".Into<FXY>()(2), 1);
        AreEqual("(((((addTest(x, 2))))))".Into<FXY>()(2), 4);
        AreEqual("addTest(x, (2))".Into<FXY>()(2), 4);
        AreEqual("addTest(x, mulTest x 2)".Into<FXY>()(2), 6);
        AreEqual("addTest(x, mulTest(x, 2))".Into<FXY>()(2), 6);
        AreEqual("addTest(x, mulTest((x), ((2))))".Into<FXY>()(2), 6);
        AreEqual("var ff::Func<float,float> = $(mulTest, addTest 3 5); ff(x);".Into<FXY>()(2), 16);
    }

    [Test]
    public static void Errors() {
        ThrowsRegex("no method by name `mulTest` that takes 1 argument", () => "addTest(x, mulTest(x 2))".Into<FXY>());
        ThrowsRegex("Couldn't convert the text in ≪≫ to type float.*≪}≫", () => StateMachine.CreateFromDump(
            @"
pattern {}
phase 0
	saction 0
		gtr {
			wait-child
			times(20)
		} {
			delay
		}
		move-target(5, io-sine, py(addTest(mod 3 4, mod 6 7)))
"));
        ThrowsRegex("Couldn't convert the text in ≪≫ to type float.*≪blargh≫", () => StateMachine.CreateFromDump(
            @"
pattern {}
phase 0
	saction 0
		gtr {
			wait-child
			times(20)
		} {
			delay blargh
		}
		move-target(5, io-sine, py(addTest(mod 3 4, mod 6 7)))
"));
        ThrowsRegex("no static method with this name was found", () => "py(addTest(mode 3 4, 5))".Into<TP>());
    }

    [Test]
    public static void GroupingErrors() {
        ThrowsRegex("The first parameter must be a Func", () => "addTest(x, (2)())".Into<FXY>());
        ThrowsRegex("Expected CloseParen", () => "modwithpause 5 (6 7) 8".Into<BPY>());
        ThrowsRegex("Expected CloseParen", () => "modwithpause(5, (6 7), 8)".Into<BPY>());
        ThrowsRegex("Expected atom", () => "mod(3 *, 5)".Into<FXY>());
    }
    
    private static Vector2 V2(float x, float y) => new Vector2(x, y);

    private const string preAggSecond = "";

    [Test]
    public static void tmp() {
        GameManagement.NewInstance(InstanceMode.NULL, InstanceFeatures.InactiveFeatures);
        AreEqual("(-1 + x + 2 * y + 3)".Into<BPY>()(new ParametricInfo() { loc = new Vector2(5, 10)}), 27);
        "(5 / 24 * dl ^ 0.8)".Into<BPY>();
        AreEqual(@"
var jt = 2
var movet = 1.5
movet + jt - 1.5".Into<FXY>()(65), 2f);
    }
    [Test]
    public static void PostAggregation() {
        AreEqual("((2) + 3)".Into<FXY>()(0), 5);
        AreEqual("(((2) + 3) * 2)".Into<FXY>()(0), 10);
        AreEqual("addTest(2 * 5, x)".Into<FXY>()(4), 14);
        AreEqual("(3 * 2 + 2)".Into<FXY>()(0), 8);
        AreEqual("(3 + x)".Into<FXY>()(7), 10);
        AreEqual("(addTest 2 mulTest 5 x)".Into<FXY>()(4), 22);
        //Works at top level!
        AreEqual("3 + x".Into<FXY>()(7), 10);
        AreEqual("(5 * 3 / 2)".Into<FXY>()(0), 7.5f);
        AreEqual("((3 * 2 + 2))".Into<FXY>()(0), 8);
        AreEqual("((2 + 3) * 4)".Into<FXY>()(0), 20);
        AreEqual("(3 * (2 + 2))".Into<FXY>()(0), 12);
        AreEqual("(3 * 2 + 2)".Into<FXY>()(0), 8);
        AreEqual("(2 + 3 * 4)".Into<FXY>()(0), 14);
        AreEqual("(2 + (3 * 4))".Into<FXY>()(0), 14);
        "Mod(2 + Mod(x + 2, x + 4), 5 + addTest(2, 3))".Into<BPY>();
        "Mod(x + 2, x + 4)".Into<BPY>();
        AreEqual("t > 5 ? 10 + 2 : 3 + 4".Into<BPY>()(new ParametricInfo(){t = 6}), 12);
    }
}
}