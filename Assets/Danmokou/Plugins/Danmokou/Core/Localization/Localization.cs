using System;
using System.Linq;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.Events;

namespace Danmokou.Core {
public static class Locales {
    public const string? EN = null;
    public const string JP = "jp";

    public static readonly string?[] AllLocales = {EN, JP};

    private static IDMKLocaleProvider? _prov;
    public static IDMKLocaleProvider Provider => _prov ??= ServiceLocator.Find<IDMKLocaleProvider>();
    public static string? TextLocale => Provider.TextLocale.Value;
}
public interface IDMKLocaleProvider {
    public Evented<string?> TextLocale { get; }
    public Evented<string?> VoiceLocale { get; }

    public ILocaleProvider AsText => new LocaleProvider(TextLocale);
    public ILocaleProvider AsVoice => new LocaleProvider(VoiceLocale);
}

/// <summary>
/// A trivial subclass of LString that uses the DMK save data's Text Locale as a resolver.
/// </summary>
public class LText : LString {
    public LText(string defaultValue, params (string locale, string value)[] variants) : 
        base(Locales.Provider.AsText, defaultValue, variants) { }

    public static LString Make(string defaultValue, params (string locale, string value)[] variants) =>
        new LText(defaultValue, variants);
}

[Serializable]
public class MutLString {
    public string en = "";
    public string? jp = null;

    public LString Freeze => new LText(en, 
        new[]{(Locales.JP, jp!)}.Where(x => !string.IsNullOrWhiteSpace(x.Item2)).ToArray());
}

[Serializable]
public class LocalizedStringReference {
    public bool useReference = true;
    public string reference = null!;
    public MutLString hardcoded = null!;
    [NonSerialized]
    private LString? _value;
    public LString? MaybeValue => _value ??= useReference ?
        LocalizedStrings.TryFindReference(reference) : hardcoded.Freeze;
    public LString ValueOrEmpty => MaybeValue ?? LString.Empty;
    public LString Value => MaybeValue ??
        throw new Exception($"Couldn't resolve LocalizedString reference {reference}");

    public LString SetDefault(LString? save) =>
        _value ??= MaybeValue ?? save ??
            throw new Exception($"Couldn't find a LocalizedString by key '{reference}'");
    public LString SetDefaultOrEmpty(LString? save) => 
        _value ??= MaybeValue ?? save ?? LString.Empty;
}
}