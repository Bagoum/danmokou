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
    }
    
    public UIResult OnConfirm() {
        OnPointerClick(null!);
        return new UIResult.StayOnNode();
    }

    public void OnEnter(UINode n) => OnPointerEnter(null!);

    public void OnLeave(UINode n) => OnPointerExit(null!);

    public void OnPointerDown(UINode n, PointerDownEvent ev) => OnPointerDown(null!);

    public void OnPointerUp(UINode n, PointerUpEvent ev) => OnPointerUp(null!);
}
}