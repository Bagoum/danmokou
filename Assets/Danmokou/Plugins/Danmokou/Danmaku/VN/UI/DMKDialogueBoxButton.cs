using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using Danmokou.UI;
using Danmokou.UI.XML;
using Danmokou.VN.Mimics;
using UnityEngine.UIElements;

namespace SuzunoyaUnity.UI {
public class DMKDialogueBoxButton : DialogueBoxButton, IFixedXMLReceiver {
    private FixedXMLHelper xml = null!;
    
    protected override void Awake() {
        base.Awake();
        xml = GetComponent<FixedXMLHelper>();
    }
    
    public void Bind(DMKADVDialogueBoxMimic parent, ADVDialogueBox db) {
        parent.Listen(db.ComputedLocation, _ => xml.UpdatedLocations());
        var isVisible = new DisturbedAnd();
        parent.AddToken(isVisible.AddDisturbance(db.Visible));
        parent.AddToken(isVisible.AddDisturbance(db.ComputedTint.Map(c => c.a > 0)));
        parent.AddToken(isVisible.AddDisturbance(db.MinimalState.Map(b => !b)));
        parent.Listen(isVisible, xml.XML.IsVisible.OnNext);
        parent.Listen(db.MinimalState, b => {
            if (b)
                FastSetState(State | ButtonState.Hide);
            else
                State &= (ButtonState.All ^ ButtonState.Hide);
        });
    }
    
    public UIResult OnConfirm(UINode n) {
        OnPointerClick(null!);
        return new UIResult.StayOnNode();
    }

    public void OnEnter(UINode n) => OnPointerEnter(null!);

    public void OnLeave(UINode n) => OnPointerExit(null!);

    public void OnPointerDown(UINode n, PointerDownEvent ev) => OnPointerDown(null!);

    public void OnPointerUp(UINode n, PointerUpEvent ev) => OnPointerUp(null!);
}
}