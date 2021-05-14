using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Data/Boss Color Scheme")]
public class BossColorScheme : ScriptableObject {
    public Color uiColor;
    public Color uiHPColor;
    /// <summary>
    /// Used for end-of-spellcard power effects
    /// </summary>
    public Color powerAuraColor;
    public Color cardColorR;
    public Color cardColorG;
    //Some of these colors are not used by the standard card/spell circles and are therefore hidden.
    [HideInInspector] public Color cardColorB;
    public Color spellColor1;
    [HideInInspector] public Color spellColor2;
    [HideInInspector] public Color spellColor3;
}
}
