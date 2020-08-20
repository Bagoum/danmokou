public static class Extensions {

    public static string Locale(this string en, string ja) {
        if (SaveData.s.Locale == global::Locale.JP) return ja;
        return en;
    }
}