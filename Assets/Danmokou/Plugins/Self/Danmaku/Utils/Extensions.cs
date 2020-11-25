using UnityEngine;

public static class Extensions {

    public static string Locale(this string en, string ja) {
        if (SaveData.s.Locale == global::Locale.JP) return ja;
        return en;
    }

    public static void SetAlpha(this SpriteRenderer sr, float a) {
        var c = sr.color;
        c.a = a;
        sr.color = c;
    }
}