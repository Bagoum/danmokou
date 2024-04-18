using Danmokou.Core;
using Danmokou.Danmaku;
using NUnit.Framework;

namespace Danmokou.Testing {
public class StyleSelectorTests {
    
    [Test]
    public void RegexTest() {
        var r1 = "red";
        Assert.IsTrue(StyleSelector.RegexMatches(r1, "red"));
        Assert.IsFalse(StyleSelector.RegexMatches(r1, "1red"));
        Assert.IsFalse(StyleSelector.RegexMatches(r1, "red1"));
        r1 = "*red";
        Assert.IsFalse(StyleSelector.RegexMatches(r1, "red"));
        Assert.IsTrue(StyleSelector.RegexMatches(r1, "123red"));
        Assert.IsFalse(StyleSelector.RegexMatches(r1, "1red1"));
        Assert.IsFalse(StyleSelector.RegexMatches(r1, "red1"));
        r1 = "red*";
        Assert.IsFalse(StyleSelector.RegexMatches(r1, "red"));
        Assert.IsFalse(StyleSelector.RegexMatches(r1, "1red"));
        Assert.IsFalse(StyleSelector.RegexMatches(r1, "1red1"));
        Assert.IsTrue(StyleSelector.RegexMatches(r1, "red123"));
        r1 = "*red*";
        Assert.IsFalse(StyleSelector.RegexMatches(r1, "red"));
        Assert.IsFalse(StyleSelector.RegexMatches(r1, "1red"));
        Assert.IsTrue(StyleSelector.RegexMatches(r1, "1red1"));
        Assert.IsFalse(StyleSelector.RegexMatches(r1, "red123"));
        r1 = "*red*blue*";
        Assert.IsFalse(StyleSelector.RegexMatches(r1, "1red2green3"));
        Assert.IsTrue(StyleSelector.RegexMatches(r1, "1red2blue3"));
    }
}
}