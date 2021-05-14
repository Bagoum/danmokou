using System;
using JetBrains.Annotations;

namespace Danmokou.Core {
public static partial class LocalizedStrings {
    public static bool IsLocalizedStringReference(string key) =>
        key.Length > 0 && key[0] == ':';
    private static string Sanitize(string reference_key) {
        if (IsLocalizedStringReference(reference_key)) reference_key = reference_key.Substring(1);
        return reference_key;
    }
    public static LocalizedString? TryFindReference(string reference_key) =>
        TryFindReference(reference_key, out var ls) ? ls : null;

    public static LocalizedString FindReference(string reference_key) =>
        TryFindReference(reference_key, out var ls) ?
            ls :
            throw new Exception($"Could not find LocalizedString reference for {reference_key}");

    public static bool TryFindReference(string reference_key, out LocalizedString ls) =>
        _allDataMap.TryGetValue(Sanitize(reference_key), out ls);
}
}