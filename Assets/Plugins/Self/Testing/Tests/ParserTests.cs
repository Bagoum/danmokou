using DMath;
using NUnit.Framework;
using UnityEngine;
using static Compilers;
using static DMath.BPYRepo;
using static NUnit.Framework.Assert;
using static Tests.TAssert;
using ExFXY = System.Func<TEx<float>, TEx<float>>;

namespace Tests {

public static class ParserTests {
    private const float err = 0.00001f;

    [Test]
    public static void CommasWork() {
        AreEqual("+(x, 2)".Into<FXY>()(2), 4);
        AreEqual("(((((+(x, 2))))))".Into<FXY>()(2), 4);
        //I guess you can call this currying?
        AreEqual("+ 8 (if = p 0) 0 6".Into<BPY>()(new ParametricInfo(Vector2.down, 5, 0)), 14);
        ThrowsMessage("no enclosing argument list", () => "(+ 8 * 5, x)".Into<FXY>());
        AreEqual("(+ 8 *(5, x))".Into<FXY>()(2), 18);
        AreEqual("*(+ 3 5)(x)".Into<FXY>()(2), 16);
        AreEqual("+(x, (2))".Into<FXY>()(2), 4);
        AreEqual("+(x, * x 2)".Into<FXY>()(2), 6);
        AreEqual("+(x, (* x 2))".Into<FXY>()(2), 6);
        ThrowsMessage("could not find a comma", () => "+(x, *(x 2))".Into<FXY>());
        AreEqual(@"<#> strict(none)
+(x, *(x 2))".Into<FXY>()(5), 15);
        ThrowsMessage("could not find a comma", () => @"<#> strict(comma)
+(x, *(x 2))".Into<FXY>());
        AreEqual("+(x, *(x, 2))".Into<FXY>()(2), 6);
        AreEqual("+(x, *((x), ((2))))".Into<FXY>()(2), 6);
        AreEqual("+ x() 3".Into<FXY>()(2), 5);
        AreEqual("+ x (3)".Into<FXY>()(2), 5);
        // (f x) y
    }

    private const string expCloseParen = "closing parentheses was expected";
    [Test]
    public static void GroupingErrors() {
        ThrowsMessage(expCloseParen, () => "+(x, (2)())".Into<FXY>());
        ThrowsMessage(expCloseParen, () => "modwithpause 5 (6 7) 8".Into<BPY>());
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
    public static void PostAggregation() {
        ThrowsMessage("could not parse the second", () => "mod(3 *, 5)".Into<FXY>());
        AreEqual("(3 + x)".Into<FXY>()(7), 10);
        AreEqual("(+ 2 * 5 x)".Into<FXY>()(4), 22);
        AreEqual("+(2 * 5, x)".Into<FXY>()(4), 14);
        ThrowsMessage("could not find a comma", () => "+(2 * 5 x)".Into<FXY>());
        //Parsing completes at 3
        AreEqual("3 + x".Into<FXY>()(7), 3);
        AreEqual("(3 * 2 + 2)".Into<FXY>()(0), 8);
        AreEqual("(5 * 3 / 2)".Into<FXY>()(0), 7.5f);
        AreEqual("(5 * 3 // 6)".Into<FXY>()(0), 2f);
        "(3 * 2 + 2)".Into<ExFXY>();
        AreEqual("((3 * 2 + 2))".Into<FXY>()(0), 8);
        AreEqual("((2) + 3)".Into<FXY>()(0), 5);
        AreEqual("(((2) + 3) * 2)".Into<FXY>()(0), 10);
        AreEqual("((2 + 3) * 4)".Into<FXY>()(0), 20);
        AreEqual("(3 * (2 + 2))".Into<FXY>()(0), 12);
        AreEqual("(3 * 2 + 2)".Into<FXY>()(0), 8);
        AreEqual("(2 + 3 * 4)".Into<FXY>()(0), 14);
        AreEqual("(2 + (3 * 4))".Into<FXY>()(0), 14);
        "Mod(2 + Mod(x + 2, x + 4), 5 + +(2, 3))".Into<BPY>();
        "Mod(x + 2, x + 4)".Into<BPY>();
    }

    [Test]
    public static void SoftIncorrectGrouping() {
        AreEqual("if(> t 5, 10 + 2, 3 + 4)".Into<BPY>()(new ParametricInfo(){t = 6}), 12);
        AreEqual("if(> t 5) (10 + 2) (3 + 4)".Into<BPY>()(new ParametricInfo(){t = 6}), 12);
    }
}
}