using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Culture;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using BagoumLib.Transitions;
using Danmokou.Core;
using Danmokou.Core.DInput;
using Danmokou.DMath;
using UnityEngine;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {

public class UIScreen : ITokenized {
    [Flags]
    public enum Display {
        Basic = 0,
        WithTabs = 1 << 0,
        Unlined = 1 << 1,
        PauseThin = 1 << 2,
        PauseLined = 1 << 3,
        
        OverlayTH = PauseThin | PauseLined
    }

    public bool ScreenIsActive { get; private set; } = false;
    public List<IDisposable> Tokens { get; } = new();
    public UIController Controller { get; }
    public Display Type { get; set; }
    private LString? HeaderText { get; }
    public List<UIGroup> Groups { get; } = new();
    public VisualElement HTML { get; private set; } = null!;
    public UIRenderScreen ScreenRender { get; }
    public UIRenderScreenContainer ContainerRender { get; }
    public UIRenderAbsoluteTerritory AbsoluteTerritory { get; private set; }
    /// <summary>
    /// Whether or not the screen can be exited via the player clicking the "back" button.
    /// </summary>
    public bool AllowsPlayerExit { get; set; } = true;
    public bool UseControlHelper { get; set; } = true;
    private Label? ControlHelper { get; set; } = null!;
    public Action<UIScreen, VisualElement>? Builder { private get; set; } = null!;
    public GameObject? SceneObjects { get; set; }
    /// <summary>
    /// Overrides the visualTreeAsset used to construct this screen's HTML.
    /// </summary>
    public VisualTreeAsset? Prefab { get; init; }
    /// <summary>
    /// Event fired when entering the screen.
    /// <br/>bool- True iff this UIScreen is being entered "from null", ie. without a transition.
    /// </summary>
    public Event<bool> OnEnterStart { get; } = new();
    public UIScreen WithOnEnterStart(Action<bool> cb) {
        Tokens.Add(OnEnterStart.Subscribe(cb));
        return this;
    }
    public Event<Unit> OnEnterEnd { get; } = new();
    public UIScreen WithOnEnterEnd(Action<Unit> cb) {
        Tokens.Add(OnEnterEnd.Subscribe(cb));
        return this;
    }
    public Event<Unit> OnExitStart { get; } = new();
    public UIScreen WithOnExitStart(Action<Unit> cb) {
        Tokens.Add(OnExitStart.Subscribe(cb));
        return this;
    }
    public Event<Unit> OnExitEnd { get; } = new();
    public UIScreen WithOnExitEnd(Action<Unit> cb) {
        Tokens.Add(OnExitEnd.Subscribe(cb));
        return this;
    }
    
    
    /// <summary>
    /// The opacity of the HTML background image of the menu containing the uiScreen.
    /// <br/>In the default setup, this is a block of dark color with opacity ~ 0.8 to lower contrast on the content behind the menu.
    /// <br/>The visibility of the menu's background is dependent on the current screen.
    /// </summary>
    public float MenuBackgroundOpacity { private get; set; }
    public const float DefaultMenuBGOpacity = 0.85f;
    
    /// <summary>
    /// The opacity of the HTML background image of the uiScreen.
    /// <br/>In the default setup, this is a patterned background with opacity 1.
    /// <br/>Note that in cases like main menus, you want to use DMK backgrounds (see <see cref="Danmokou.UI.XML.XMLHelpers.WithBG"/>).
    /// </summary>
    public float BackgroundOpacity { private get; set; }

    private readonly PushLerper<float> backgroundOpacity = 
        new(0.5f, (a, b, t) => M.Lerp(a, b, Easers.EIOSine(t)));

    /// <summary>
    /// Link to the UXML object to which screen-specific columns, rows, etc. can be added.
    /// <br/>By default, this is padded 480 left and right.
    /// </summary>
    public VisualElement Container => HTML.Q("Container") ?? HTML;
    public Label Header => HTML.Q<Label>("Header");
    public VisualElement Margin => HTML.Q("MarginContainer") ?? HTML;

    //public UIRenderDirect Renderer { get; }

    private Dictionary<Type, VisualTreeAsset>? buildMap;

    public UIScreen(UIController controller, LString? header, Display display = Display.Basic) {
        Controller = controller;
        HeaderText = header;
        Type = display;
        ScreenRender = new UIRenderScreen(this);
        ContainerRender = new UIRenderScreenContainer(this);
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
        HTML = (Prefab != null ? Prefab : map.SearchByType(this, true)).CloneTreeNoContainer();
        if (HeaderText == null)
            // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
            Header?.parent.Remove(Header);
        else
            ServiceLocator.Find<IDMKLocaleProvider>().TextLocale
                .Subscribe(_ => Header.text = HeaderText.CSpace());
        if (Type.HasFlag(Display.Unlined))
            HTML.AddToClassList("unlined");
        if (Type.HasFlag(Display.WithTabs))
            throw new Exception("I haven't written tab CSS yet");
        if (Type.HasFlag(Display.PauseThin))
            HTML.AddToClassList("pauseThin");
        if (Type.HasFlag(Display.PauseLined))
            HTML.AddToClassList("pauseLined");
        HTML.Add(XMLUtils.Prefabs.AbsoluteTerritory.CloneTreeNoContainer());
        AbsoluteTerritory = new UIRenderAbsoluteTerritory(this);
        Builder?.Invoke(this, Container);
        //Controls helper may be removed by builder for screens that don't need it
        ControlHelper = HTML.Q<Label>("ControlsHelper");
        if (!UseControlHelper) {
            ControlHelper?.RemoveFromHierarchy();
            ControlHelper = null;
        }
        //calling build may awaken lazy nodes, causing new groups to spawn
        for (int ii = 0; ii < Groups.Count; ++ii)
            Groups[ii].Build(map);
        SetVisible(false);
        Controller.AddToken(Controller.UIVisualUpdateEv.Subscribe(VisualUpdate));
        Controller.AddToken(backgroundOpacity.Subscribe(f => HTML.style.unityBackgroundImageTintColor = 
            new Color(1,1,1,f)));
        backgroundOpacity.Push(0);
        return HTML;
    }

    private void SetVisible(bool visible) {
        HTML.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (SceneObjects != null)
            SceneObjects.SetActive(visible);
        SetControlText();
    }

    public UIRenderColumn ColumnRender(int index) => new(this, index);

    public void ExitStart() {
        ScreenIsActive = false;
        OnExitStart.OnNext(default);
    }
    public void ExitEnd() {
        SetVisible(false);
        OnExitEnd.OnNext(default);
    }
    public void EnterStart(bool fromNull) {
        ScreenIsActive = true;
        SetVisible(true);
        Controller.BackgroundOpacity.Push(MenuBackgroundOpacity);
        backgroundOpacity.Push(BackgroundOpacity);
        OnEnterStart.OnNext(fromNull);
    }
    public void EnterEnd() {
        OnEnterEnd.OnNext(default);
    }

    public void VisualUpdate(float dT) {
        backgroundOpacity.Update(dT);
        SetControlText();
    }

    private void SetControlText() {
        var inp = InputManager.PlayerInput.MainSource.Current;
        string AsControl(IInputHandler h) => StringBuffer.FormatPooled("{0}: {1}", h.Purpose.Value, h.Description);
        if (HTML.style.display == DisplayStyle.Flex && ControlHelper != null)
            ControlHelper.text = 
                StringBuffer.JoinPooled("    ", AsControl(inp.uiConfirm), AsControl(inp.uiBack));
    }

    /// <summary>
    /// Mark all nodes on this screen as destroyed.
    /// <br/>Does not affect HTML (call <see cref="DestroyScreen"/> instead to destroy HTML).
    /// Call this method when the menu containing this screen is being destroyed.
    /// </summary>
    public void MarkScreenDestroyed() {
        foreach (var g in Groups)
            g.MarkNodesDestroyed();
        Tokens.DisposeAll();
    }

    /// <summary>
    /// Mark all nodes on this screen as destroyed, and delete this screen's HTML.
    /// </summary>
    public void DestroyScreen() {
        MarkScreenDestroyed();
        HTML.RemoveFromHierarchy();
    }

    public static implicit operator UIRenderSpace(UIScreen s) => s.ContainerRender;
}
}