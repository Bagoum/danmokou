using System;
using JetBrains.Annotations;

namespace DMK.Core {
public enum Locale {
    EN,
    JP
}

public static class Localization {
    public static Locale Locale { get; set; } = Locale.EN;
}

public class Localized<T> where T : class {
    public T en;
    public T jp;
    
    public Localized(T en, T jp = null) {
        this.en = en;
        this.jp = jp;
    }

    [CanBeNull]
    public T Value {
        get {
            switch (Localization.Locale) {
                case Locale.EN:
                    return en;
                case Locale.JP:
                    return jp;
                default:
                    return null;
            }
        }
    }

    public T ValueOrEn => Value ?? en;
}
[Serializable]
public class LocalizedString : Localized<string> {
    //For editor display usage
    public bool _showEnOnly;

    public LocalizedString(string en, string jp = null) : base(en, jp) {
        _showEnOnly = false;
    }

    public static implicit operator string(LocalizedString ls) => ls.ValueOrEn;
    public override string ToString() => this;

    public LocalizedString Or(LocalizedString other) => new LocalizedString(en.Or(other.en), jp.Or(other.jp));
    public LocalizedString OrSame(string other) => new LocalizedString(en.Or(other), jp.Or(other));
}
}