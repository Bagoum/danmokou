using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {
public abstract record UIRenderSpace(NUIScreen Screen) {
    private List<UIGroup> Groups { get; } = new();
    public abstract VisualElement HTML { get; }

    public void AddGroup(UIGroup grp) => Groups.Add(grp);
    
    public void RemoveGroup(UIGroup grp) => Groups.Remove(grp);
    public void HideAllGroups() {
        foreach (var g in Groups)
            g.Hide();
    }
}

public record UIRenderScrollColumn(NUIScreen Screen, int Index) : UIRenderSpace(Screen) {
    private VisualElement? _html = null;
    public override VisualElement HTML => _html ??= Screen.HTML.Query<ScrollView>().ToList()[Index];
}

}