using System;
using System.Linq;
using JetBrains.Annotations;

namespace Danmokou.Core {
public enum Locale {
    EN,
    JP
    //The methods Localized<T>.ValueForLocale and LocalizedString.Format must be updated
    // if this is extended.
}

public static class Localization {
    public static Locale Locale { get; set; } = Locale.EN;
}

public abstract class Localized : ILangObject {
    public abstract object ObjValueOrEnForLocale(Locale l);
}
public class Localized<T> : Localized where T : class {
    public T en = default!;
    public T? jp;

    public Localized() { }
    public Localized(T en, T? jp = null) {
        this.en = en;
        this.jp = jp;
    }

    public T? ValueForLocale(Locale l) => l switch {
        Locale.EN => en,
        Locale.JP => jp,
        _ => null
    };
    public T? Value => ValueForLocale(Localization.Locale);
    public override object ObjValueOrEnForLocale(Locale l) => ValueOrEnForLocale(l);

    public T ValueOrEnForLocale(Locale l) => ValueForLocale(l) ?? en;
    public T ValueOrEn => Value ?? en;
    private (T, T?) Tuple => (en, jp);

    public static bool operator ==(Localized<T>? a, Localized<T>? b) => a?.Tuple == b?.Tuple;
    public static bool operator !=(Localized<T>? a, Localized<T>? b) => !(a == b);
    public override bool Equals(object obj) => obj is Localized<T> l && l == this;
    public override int GetHashCode() => Tuple.GetHashCode();
}
[Serializable]
public class LocalizedString : Localized<string> {

    public LocalizedString() { }
    public LocalizedString(string en, string? jp = null) : base(en, jp) {}

    public static implicit operator string(LocalizedString ls) => ls.ValueOrEn;
    //Enable this conversion if it's convenient to use hardcoded strings in your codebase.
    public static implicit operator LocalizedString(string x) => new LocalizedString(x);
    public override string ToString() => this;

    public static LocalizedString All(string allLangs) => new LocalizedString(allLangs, allLangs);
    public LocalizedString Or(LocalizedString other) => new LocalizedString(en.Or(other.en)!, jp.Or(other.jp));

    public override bool Equals(object obj) => obj is LocalizedString ls && this == ls;
    public override int GetHashCode() => base.GetHashCode();

    public static LocalizedString Empty => new LocalizedString("");

    private static string Format(Locale l, string fmtString, params LocalizedString[] args) =>
        string.Format(fmtString, args.Select(a => (object)a.ValueOrEnForLocale(l)).ToArray());

    public static LocalizedString Format(string fmtString, params LocalizedString[] args) =>
        Format(new LocalizedString(fmtString), args);
    public static LocalizedString Format(LocalizedString fmtString, params LocalizedString[] args) =>
        new LocalizedString(Format(Locale.EN, fmtString.ValueOrEnForLocale(Locale.EN), args)) {
            jp = Format(Locale.JP, fmtString.ValueOrEnForLocale(Locale.JP), args)
        };

    public LocalizedString FMap(Func<string, string> mapper) => new LocalizedString(mapper(en)) {
        jp = jp == null ? null : mapper(jp)
    };
}

[Serializable]
public class LocalizedStringReference {
    public bool useReference = true;
    public string reference = null!;
    public LocalizedString hardcoded = null!;
    [NonSerialized]
    private LocalizedString? _value;
    public LocalizedString? MaybeValue => _value ??= useReference ?
        LocalizedStrings.TryFindReference(reference) : hardcoded;
    public LocalizedString ValueOrEmpty => MaybeValue ?? new LocalizedString("");
    public LocalizedString Value => MaybeValue ??
        throw new Exception($"Couldn't resolve LocalizedString reference {reference}");
    
    public LocalizedString SetDefault(LocalizedString? save) => 
        _value ??= MaybeValue ?? save ?? 
            throw new Exception("Couldn't load LocalizedString as a reference, hardcoded value, or default");
    public LocalizedString SetDefaultOrEmpty(LocalizedString? save) => 
        _value ??= MaybeValue ?? save ?? LocalizedString.Empty;
}
}