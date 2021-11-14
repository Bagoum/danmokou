using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.Mathematics;
using BagoumLib.Tweening;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Graphics.Backgrounds;
using Danmokou.Services;
using UnityEngine;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {

public class UIScreen {
    [Flags]
    public enum Display {
        Basic = 0,
        WithTabs = 1 << 0,
        Unlined = 1 << 1,
        OverlayTH = 1 << 2,
    }
    public UIController Controller { get; }
    public Display Type { get; }
    private LString? HeaderText { get; }
    public List<UIGroup> Groups { get; } = new();
    public VisualElement HTML { get; private set; } = null!;
    public UIRenderDirect DirectRender { get; private set; } = null!;
    public UIRenderAbsoluteTerritory AbsoluteTerritory { get; private set; } = null!;
    public Action<UIScreen, VisualElement>? Builder { private get; init; } = null!;
    public GameObject? SceneObjects { get; set; }
    /// <summary>
    /// Overrides the visualTreeAsset used to construct this screen's HTML.
    /// </summary>
    public VisualTreeAsset? Prefab { get; init; }
    public Action? OnExitStart { private get; init; }
    public Action? OnExitEnd { private get; init; }
    public Action? OnEnterStart { private get; init; }
    public Action? OnEnterEnd { private get; init; }
    /// <summary>
    /// The menu container may define background handling instead of being transparent.
    /// <br/>The visibility of the menu's background is dependent on the current screen.
    /// </summary>
    public float BackgroundOpacity { private get; set; }
    
    public (GameObject prefab, BackgroundTransition transition)? Background { private get; set; }

    /// <summary>
    /// Link to the UXML object to which screen-specific columns, rows, etc. can be added.
    /// <br/>By default, this is padded 480 left and right.
    /// </summary>
    public VisualElement Container => HTML.Q("Container");
    private Label Header => HTML.Q<Label>("Header");
    public VisualElement Margin => HTML.Q("MarginContainer");

    public UIScreen WithBG((GameObject, BackgroundTransition)? bgConfig) {
        Background = bgConfig;
        return this;
    }

    private readonly IBackgroundOrchestrator? bgo;
    
    private Dictionary<Type, VisualTreeAsset>? buildMap;

    public UIScreen(UIController controller, LString? header, Display display = Display.Basic) {
        Controller = controller;
        HeaderText = header;
        Type = display;
        bgo = ServiceLocator.MaybeFind<IBackgroundOrchestrator>();
    }
    
    public void AddGroup(UIGroup grp) {
        Groups.Add(grp);
        if (buildMap != null)
            grp.Build(buildMap);
    }

    /// <summary>
    /// Reorder the groups attached to this screen such that the provided group is first.
    /// </summary>
    public void SetFirst(UIGroup group) {
        Groups.Remove(group);
        Groups.Insert(0, group);
    }

    public VisualElement Build(Dictionary<Type, VisualTreeAsset> map) {
        buildMap = map;
        HTML = (Prefab != null ? Prefab : map.SearchByType(this, true)).CloneTreeWithoutContainer();
        if (HeaderText == null)
            Header.style.display = DisplayStyle.None;
        else
            Header.text = HeaderText;
        if (Type.HasFlag(Display.Unlined))
            HTML.AddToClassList("unlined");
        if (Type.HasFlag(Display.WithTabs))
            throw new Exception("I haven't written tab CSS yet");
        if (Type.HasFlag(Display.OverlayTH))
            HTML.AddToClassList("overlayTH");
        HTML.Add(GameManagement.References.uxmlDefaults.AbsoluteTerritory.CloneTreeWithoutContainer());
        AbsoluteTerritory = new UIRenderAbsoluteTerritory(this);
        DirectRender = new UIRenderDirect(this);
        Builder?.Invoke(this, Container);
        //calling build may awaken lazy nodes, causing new groups to spawn
        for (int ii = 0; ii < Groups.Count; ++ii)
            Groups[ii].Build(map);
        SetVisible(false);
        return HTML;
    }

    private void SetVisible(bool visible) {
        HTML.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (SceneObjects != null)
            SceneObjects.SetActive(visible);
    }

    public UIRenderColumn ColumnRender(int index) => new(this, index);

    //TODO: merge exit/postexit and preenter/enter as part of seamless transition
    

    public void ExitStart() {
        OnExitStart?.Invoke();
        foreach (var g in Groups)
            g.ScreenExitStart();
    }
    public void ExitEnd() {
        SetVisible(false);
        OnExitEnd?.Invoke();
        //Groups may destroy themselves during exit
        foreach (var g in Groups.ToList())
            g.ScreenExitEnd();
    }
    public void EnterStart(bool fromNull) {
        SetVisible(true);
        Controller.BackgroundOpacity.Push(BackgroundOpacity);
        if (Background.Try(out var bg)) {
            bgo?.QueueTransition(bg.transition);
            bgo?.ConstructTarget(bg.prefab, !fromNull);
        }
        OnEnterStart?.Invoke();
        foreach (var g in Groups)
            g.ScreenEnterStart();
    }
    public void EnterEnd() {
        OnEnterEnd?.Invoke();
        foreach (var g in Groups)
            g.ScreenEnterEnd();
    }
    
}
}