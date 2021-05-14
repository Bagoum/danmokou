using System;

namespace Danmokou.Core {
public static class LocalizationRendering {
    public static string Render(Locale locale, string[] pieces, params object[] fmtArgs) {
        for (int ii = 0; ii < fmtArgs.Length; ++ii) {
            if (fmtArgs[ii] is Localized l) {
                fmtArgs[ii] = l.ObjValueOrEnForLocale(locale);
            }
        }
        if (pieces.Length == 1) {
            if (fmtArgs.Length == 0)
                return pieces[0];
            else
                return string.Format(pieces[0], fmtArgs);
        } else
            return string.Format(string.Join("", pieces), fmtArgs);
    }

    public static string PLURAL(object arg, string singular, string plural) =>	
        arg.Cast<ILangCountable>().ResolveOneMany(singular, plural);
	
    public static string JP_COUNTER(object arg, string counter_type) {
        var obj = arg.Cast<ILangCountable>();
        //this could be more complex, imagine if you were exporting in hiragana, so you'd actually
        // need to switch over the counter_type to determine which spelling of the number to use
        return $"{obj.Count}{counter_type}";
    }


    private static T Cast<T>(this object obj) {
        if (obj is T t) return t;
        throw new Exception($"Localization ({Localization.Locale}): {obj} is not of type {typeof(T)}");
    }
}
}