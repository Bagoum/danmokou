using System;
using System.Linq;
using BagoumLib.Culture;
using JetBrains.Annotations;

namespace Danmokou.Core {
public static class Locales {
    public const string? EN = null;
    public const string JP = "jp";

    public static readonly string?[] AllLocales = {EN, JP};
}

[Serializable]
public class MutLString {
    public string en = "";
    public string? jp = null;

    public LString Freeze => new LString(en, 
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
    public LString ValueOrEmpty => MaybeValue ?? new LString("");
    public LString Value => MaybeValue ??
        throw new Exception($"Couldn't resolve LocalizedString reference {reference}");
    
    public LString SetDefault(LString? save) => 
        _value ??= MaybeValue ?? save ?? 
            throw new Exception("Couldn't load LocalizedString as a reference, hardcoded value, or default");
    public LString SetDefaultOrEmpty(LString? save) => 
        _value ??= MaybeValue ?? save ?? LString.Empty;
}
}