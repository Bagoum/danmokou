using System.Collections.Generic;
using Danmokou.Core;
using Danmokou.Scriptables;
using UnityEngine;

namespace Danmokou.Core {
public static class ColorScheme {
    private static readonly Dictionary<string, Palette> palettes = new Dictionary<string, Palette>();

    public static void LoadPalettes(Palette[] ps) {
        foreach (var p in ps) {
            palettes[p.colorName] = p;
        }
    }

    public static Vector4 GetColor(string palette, Palette.Shade shade = Palette.Shade.PURE) =>
        palettes.GetOrThrow(palette, "Palettes").GetColor(shade);
}
}