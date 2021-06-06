using System.Collections.Generic;
using BagoumLib.Culture;
using NUnit.Framework;
using Danmokou.Core;
using static Danmokou.Core.LocalizedStrings.TestContent1;

namespace Danmokou.Testing {
public static class LocalizationTests {

    [Test]
    public static void TestBasic() {
        var kasen = new LString("Kasen", (Locales.JP, "華扇"));
        Localization.Locale.Value = Locales.EN;
        Assert.AreEqual("Kasen picked up 50 gold coins", pickup_gold(kasen, 50));
        Assert.AreEqual("Kasen picked up 1 gold coin", pickup_gold(kasen, 1));
        Assert.AreEqual("Kasen picked up {1} gold coins", escape_example(kasen, 50));
        Assert.AreEqual("Kasen picked up {1} gold coin", escape_example(kasen, 1));
        Localization.Locale.Value = Locales.JP;
        Assert.AreEqual("華扇が金貨を50枚拾いました", pickup_gold(kasen, 50));
        Assert.AreEqual("華扇が金貨を1枚拾いました", pickup_gold(kasen, 1));
        Assert.AreEqual("華扇が金貨を{$JP_COUNTER(1, 枚)}拾いました", escape_example(kasen, 50));
        Assert.AreEqual("華扇が金貨を{$JP_COUNTER(1, 枚)}拾いました", escape_example(kasen, 1));
    }

}
}