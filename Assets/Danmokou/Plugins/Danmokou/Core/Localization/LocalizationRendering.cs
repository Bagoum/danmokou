using System;
using BagoumLib.Culture;

namespace Danmokou.Core {
public static class LocalizationRendering {

    public static string PLURAL(object arg, string singular, string plural) =>
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        Convert.ToDouble(arg) == 1 ? singular : plural;
	
    public static string JP_COUNTER(object arg, string counter_type) {
        //this could be more complex, imagine if you were exporting in hiragana, so you'd actually
        // need to switch over the counter_type to determine which spelling of the number to use
        return $"{arg}{counter_type}";
    }


    private static T Cast<T>(this object obj) {
        if (obj is T t) return t;
        throw new Exception($"Localization ({Localization.Locale}): {obj} is not of type {typeof(T)}");
    }
}
}