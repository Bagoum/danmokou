using System;
using System.Collections.Generic;
using Danmokou.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {
/*
BossPracticeScreen = new UIScreen(this) { OnPreExit = ... };
var bossCol0 = new UIRenderScrollColumn(BossPracticeScreen, 0);
var bossCol1 = new UIRenderScrollColumn(BossPracticeScreen, 1);
var bossesGroup = new UIGroup(BossPracticeScreen, bossCol0) {
    LazyNodes = () => PBosses.Select(boss => 
        new UINode(boss.boss.BossPracticeName) {
            ShowHideGroup = new UIGroup(BossPracticeScreen, bossCol1, boss.Phases.Select(phase => {
                var req...
                return new UINode(...)
            }))
        }
    )
}
 */
public class NUIScreen {
    public UIController Controller { get; }
    public List<UIGroup> Groups { get; } = new();
    public VisualElement HTML { get; private set; } = null!;
    public GameObject? SceneObjects { get; init; }
    /// <summary>
    /// Overrides the visualTreeAsset used to construct this screen's HTML.
    /// </summary>
    public VisualTreeAsset? Prefab { get; init; }
    public Action? OnPreExit { get; init; }
    public Action? OnExit { get; init; }
    public Action? OnPreEnter { get; init; }
    public Action? OnEnter { get; init; }
    public Action? OnPostEnter { get; init; }

    public NUIScreen(UIController controller) {
        Controller = controller;
    }
    
    public void AddGroup(UIGroup grp) => Groups.Add(grp);

    public VisualElement Build(Dictionary<Type, VisualTreeAsset> buildMap) {
        HTML = (Prefab != null ? Prefab : buildMap.SearchByType(this, true)).CloneTree();
        foreach (var group in Groups)
            group.Build(buildMap);
        return HTML;
    }

    public void SetVisible(bool visible) {
        HTML.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (SceneObjects != null)
            SceneObjects.SetActive(visible);
    }
    
}
}