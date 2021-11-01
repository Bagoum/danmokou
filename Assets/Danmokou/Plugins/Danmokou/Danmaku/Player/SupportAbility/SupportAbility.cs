using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.UI;
using UnityEngine;

namespace Danmokou.Player {
public abstract class SupportAbility {
    public virtual bool UsesMeter => false;
    public virtual bool UsesBomb => false;

    public GameObject? cutin;
    public GameObject? spellTitle;
    public LString title = LString.Empty;
    public LString shortTitle = LString.Empty;
    
    public Color spellColor1 = Color.clear;
    public Color spellColor2 = Color.clear;

    public void SpawnCutin() {
        if (cutin != null)
            Object.Instantiate(cutin);
        if (spellTitle != null) {
            Object.Instantiate(spellTitle).GetComponent<PlayerSpellTitle>().Initialize(title, spellColor1, spellColor2);
        }
        
    }
}
}