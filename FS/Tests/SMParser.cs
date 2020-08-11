using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using static FParser.SMParser;
using static Common.Types;

namespace ParserTests {
public class Tests {
    private static string Clean(string s) {
        return s.ToArray().Where(x => !Char.IsWhiteSpace(x)).ToString();
    }
    private static void Contains(string src, string target) {
        Assert.IsTrue(Clean(src).Contains(Clean(target)));
    }
    [SetUp]
    public void Setup() { }
    private static void AssertSMEq(string source, string desired) {
        System.MathF.Floor(5f);
        desired = desired.Replace("\r", "").Trim();
        switch (lSMParser2.Invoke(source)) {
            case Errorable<string>.OK sm:
                Assert.AreEqual(desired, sm.Item.Replace(" \n ", "\n").Trim());
                break;
            case Errorable<string>.Failed errs:
                Assert.Fail(string.Join("\n", errs.Item));
                break;
            default:
                Assert.Fail();
                break;
        }
    }

    private static string FailString(string src) {
        switch (lSMParser2.Invoke(src)) {
            case Errorable<string>.OK sm:
                Assert.Fail($"Expected SM parse to fail, but got {sm.Item}");
                break;
            case Errorable<string>.Failed errs:
                Console.WriteLine($"---\n{string.Join("\n", errs.Item)}");
                return string.Join("\n", errs.Item);
            default:
                Assert.Fail();
                break;
        }
        return null;
    }
    [Test]
    public void TestNoArgInvoke() {
        AssertSMEq(@"!!{ y -5
$y $y()
", "-5 -5");
    }
    [Test]
    public void TestPostfix() {
        AssertSMEq(@"!!{ y -5
!!{ x 20
[$y]$x()
", "20 -5");
        AssertSMEq(@"[x y].z
", ".z x y");
        AssertSMEq(@"!{
block()
[x y].z
!}
$block()
", ".z x y");
    }
    [Test]
    public void TestNoSpace() {
        AssertSMEq(@"
!{ m(n)
    *%n;%n>
    %n;%n
!}
$m(5)
", @"*5;5>
5;5");
    }

    [Test]
    public void TestComment() {
        AssertSMEq(@"hello ##Comment
world #Comment
", "hello\nworld");
    }

    [Test]
    public void TestRef() {
        AssertSMEq(@"&r [&z].y & t
", "&r .y &z & t");
    }
    [Test]
    public void TestArray() {
        AssertSMEq(@"{{ a }} { { b } }
", "{ { a } } { { b } }");
    }
    [Test]
    public void TestEOF() {
        AssertSMEq(@"hello
///
w$or]ld(
", "hello");
    }
    [Test]
    public void TestQuote() {
        AssertSMEq(@"`#!$[)` `` w
///
world
", "#!$[)  w");
    }
    [Test]
    public void TestMacro() {
        AssertSMEq("action block 0\r\n!{\r\nintob(method)\r\nbullet sun-red/w <> cre 5 6 <> s tprot %method 1\r\n!}\r\n$intob(cfff)", "action block 0\nbullet sun-red/w <> cre 5 6 <> s tprot cfff 1");
    }

    [Test]
    public void TestMacroBlock() {
        AssertSMEq(@"a
!{ f(x)
%x
!}
$f(line1
line2)
", @"a
line1
line2");
    }

    [Test]
    public void TestMacroDefault() {
        AssertSMEq(@"
!{ f(x 5, y 4)
+ %x %y
!}
$f(3, 2)
$f
$f(3)
", @"+ 3 2
+ 5 4
+ 3 4");
        AssertSMEq(@"
!{ f(x, y * 2 4)
+ %x %y
!}
$f(3)
", @"+ 3 * 2 4");
    }
    
    [Test]
    public void TestMacroLine() {
        AssertSMEq(@"a
!!{x 5
$x
", @"a
5");
        AssertSMEq(@"a
!{ x() 
5
!}
$x
", @"a
5");
    }

    [Test]
    public void TestNestMacro() {
        AssertSMEq(@"!{ AddX(f)
x %f x
!}
!{ AddY(f)
y %f y
!}
!{ AddZ(f)
z %f z
!}
$AddX($AddY($AddZ(1)))
", @"x y z 1 z y x");
        AssertSMEq(@"!{ AddX(x, f)
%x%%f%
!}
$AddX(x, $AddX(` !HELLO WORLD! `, $AddX(z, 1)))
", @"x !HELLO WORLD! z1");
    }

    [Test]
    public void TestNewline() {
        AssertSMEq(@"action
!{
a()
me
!}
$a()

", @"action
me
");
    }

    [Test]
    public void TestPartial1() {
        AssertSMEq(@"!{
add(x,y)
+ %x %y
!}
!{
apply5(func)
$%func(5)
!}
$apply5($add(!$, 4))
", @"+ 5 4
");
        AssertSMEq(@"!{ add(x,y)
+ %x %y
!}
!{ apply5(func)
$%func(5)
!}
!{ apply6(func)
$%func(6)
!}
$apply6($apply5($add(!$, !$)))
", @"+ 5 6
");
        AssertSMEq(@"!{ fadd(x,fz,y)
+ %x $%fz(%y)
!}
!{ double(x)
* 2 %x
!}
!{
apply5(func)
$%func(5)
!}
$fadd(6, $double(!$), 4)
$apply5($fadd(!$, $double(!$), 4))
$apply5($fadd(9, $double(!$), !$))
", @"+ 6 * 2 4
+ 5 * 2 4
+ 9 * 2 5
");
    }

    [Test]
    public void TestProperty() {
        AssertSMEq(@"
<!>hello new world
yeet
", $@"
{PROP_KW} hello new world
yeet
");
    }

    //[Test]
    public void TestInnerLambdaNotYetImplemented() {
        //To implement this, in the Realize step, you need to check if the constructed tree has any PartialInvokes,
        //and if they do, pull all the PartialInvokes to the top and join them.
        //It's actually unreasonably difficult. It's much easier to just declare the extra params and allow the invoker
        //to !$ them. ie. !{ fadd2(x;fz;y) -> $fadd(1; $double(!$); !$). This works in the previous test.
        AssertSMEq(@"!{ fadd2(x;fz)
+ x $%fz(!$)
!}
!{ double(x)
* 2 %x
!}
!{
apply5(func)
$%func(5)
!}
$apply5($fadd2(9; $double(!$)))
", @"+ 5 * 2 4");
    }

    [Test]
    public void TestPartialBad() {
        Contains(FailString(@"!{
add(x;y)
+ %x %y
!}
!{
apply5(func)
$%func(5)
!}
$apply5($add(!$; !$))
"), "2 required");
        Contains(FailString(@"!{
add(x;y)
+ %x %y
!}
!{
apply5(func)
$%func(5)
!}
$apply5($apply5($add(!$; 4)))
"), "must be a partial macro invocation");
        var f = FailString(@"!{
add(x;y)
+ %x %y
!}
!{
apply56(func)
$%func(5; 6)
!}
$apply56($add(!$; 4))
");
        Contains(f, "\"apply56\" provides too many arguments to partial macro \"add\"");
        Contains(f, "2 provided, 1 required");
    }

    [Test]
    public void TestBad() {
        Contains(FailString(@"
%hello
"), "hello");
        Contains(FailString(@"
!{ add(x;y)
+ %x %z
!}
$add(4; 5)
"), "nonexistent variable \"%z");
        Contains(FailString(@"
!{ add(x;y)
+ %x $%z(%y)
!}
$add(4; 5)
"), "nonexistent reinvocation \"$%z");
        Contains(FailString(@"
!{ add(x;y)
+ %x %y
!}
$add(4)
"), "requires 2 arguments (1 provided)");
        Contains(FailString(@"
!{ add(x;y)
+ %x %y
!}
$add(4;5;6)
"), "requires 2 arguments (3 provided)");
        Contains(FailString(@"$add(4;5;6)
"), "No macro exists");
    }

    [Test]
    public void IllegalUnit() {
        Contains(FailString(@"%hello
"), "macro variable \"%hello\"");
        Contains(FailString(@"!$
"), "unbound macro argument");
    }
    
}
}