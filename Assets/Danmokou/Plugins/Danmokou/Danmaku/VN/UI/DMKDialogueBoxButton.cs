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
    
    private void Awake() {
        xml = GetComponent<FixedXMLHelper>();
        xml.Receiver = this;
    }
    
    public void Bind(DMKADVDialogueBoxMimic parent, ADVDialogueBox db) {
        var isVisible = new DisturbedAnd();
        parent.Listen(isVisible, xml.XML.IsVisible);
        parent.AddToken(isVisible.AddDisturbance(db.Visible));
        parent.AddToken(isVisible.AddDisturbance(db.ComputedTint.Map(c => c.a > 0)));
        parent.AddToken(isVisible.AddDisturbance(db.MinimalState.Map(b => !b)));
        parent.AddToken(IsInteractable.AddDisturbance(isVisible));
        parent.Listen(IsInteractable, x => xml.XML.IsInteractable.BaseValue = x);
        parent.Listen(db.MinimalState, b => {
            if (b)
                FastSetState(State | ButtonState.Hide);
            else
                State.Value &= (ButtonState.All ^ ButtonState.Hide);
        });
    }

    UIResult? IFixedXMLReceiver.Navigate(UINode n, ICursorState cs, UICommand req) {
        if (req == UICommand.Confirm) {
            OnPointerClick(null!);
            return new UIResult.StayOnNode();
        }
        return null;
    }

    void IFixedXMLReceiver.OnEnter(UINode n, ICursorState cs) => OnPointerEnter(null!);

    void IFixedXMLReceiver.OnLeave(UINode n, ICursorState cs) => OnPointerExit(null!);

    void IFixedXMLReceiver.OnPointerDown(UINode n, PointerDownEvent ev) => OnPointerDown(null!);

    void IFixedXMLReceiver.OnPointerUp(UINode n, PointerUpEvent ev) => OnPointerUp(null!);
}
}