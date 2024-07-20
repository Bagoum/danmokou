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

public enum UIScreenState {
    Inactive = 0, //exitEnd, HTML made invisible
    ActiveGoingInactive = 1, //exitStart
    ActiveWillGoInactive = 2, //set before exit starts; render spaces are set to ShouldBeVisibleInTree=false
                              //and prohibited from animating out
    InactiveWillGoActive = 3,
    InactiveGoingActive = 4, //enterStart, HTML made visible
    Active = 5, //enterEnd
}

public class UIScreen : ITokenized {
    [Flags]
    public enum Display {
        Default = 0,
        WithTabs = 1 << 0,
        Unlined = 1 << 1,
        PauseThin = 1 << 2,
        PauseLined = 1 << 3,
        
        OverlayTH = PauseThin | PauseLined
    }

    public Evented<UIScreenState> State { get; } = new(UIScreenState.Inactive);
    public List<IDisposable> Tokens { get; } = new();
    public UIController Controller { get; }
    public Display Type { get; set; }
    private LString? HeaderText { get; }
    public List<UIRenderSpace> Renderers { get; } = new();
    public List<UIGroup> Groups { get; } = new();
    public VisualElement HTML { get; private set; } = null!;
    public UIRenderScreen ScreenRender { get; }
    public UIRenderSpace ContainerRender { get; }
    public UIRenderAbsoluteTerritory AbsoluteTerritory { get; private set; }

    private bool _persistent = false;
    /// <summary>
    /// If true, this screen will always be visible, even if navigation is on a different screen.
    /// <br/>The screen can be interactable. However, mouse events will not trigger on
    ///  this screen's nodes when it is not the current screen,
    ///  because UIController does not allow cross-screen UIPointerCommand.Goto events.
    /// </summary>
    public bool Persistent {
        get => _persistent;
        set {
            // ReSharper disable once AssignmentInConditionalExpression
            if (_persistent = value) {
                if (Built)
                    SetVisible(true);
                State.PublishIfNotSame(UIScreenState.Active);
            }
            
        }
    }

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

    public UIScreen WithOnStateChange(UIScreenState state, Action cb) {
        Tokens.Add(State.OnChange.Subscribe(s => {
            if (s == state)
                cb();
        }));
        return this;
    }
    public UIScreen WithOnEnterStart(Action cb) => WithOnStateChange(UIScreenState.InactiveGoingActive, cb);
    public UIScreen WithOnEnterEnd(Action cb) => WithOnStateChange(UIScreenState.Active, cb);
    public UIScreen WithOnExitStart(Action cb) => WithOnStateChange(UIScreenState.ActiveGoingInactive, cb);
    public UIScreen WithOnExitEnd(Action cb) => WithOnStateChange(UIScreenState.Inactive, cb);
    public void NextState(UIScreenState state) {
        if (!Persistent)
            State.PublishIfNotSame(state);
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
    public bool Built => buildMap != null;

    public UIScreen(UIController controller, LString? header = null, Display display = Display.Default) {
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
        foreach (var render in Renderers)
            _ = render.HTML;
        //Controls helper may be removed by builder for screens that don't need it
        ControlHelper = HTML.Q<Label>("ControlsHelper");
        if (!UseControlHelper) {
            ControlHelper?.RemoveFromHierarchy();
            ControlHelper = null;
        }
        buildMap = map;
        //calling build may awaken lazy nodes, causing new groups to spawn
        for (int ii = 0; ii < Groups.Count; ++ii)
            Groups[ii].Build(map);
        Tokens.Add(State.OnChange.Subscribe(s => {
            if (s == UIScreenState.Inactive)
                SetVisible(false);
            else if (s == UIScreenState.InactiveGoingActive)
                SetVisible(true);
        }));
        SetVisible(State >= UIScreenState.InactiveGoingActive);
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
        if (visible) {
            Controller.BackgroundOpacity.Push(MenuBackgroundOpacity);
            backgroundOpacity.Push(BackgroundOpacity);
        }
    }

    public UIRenderColumn ColumnRender(int index) => new(this, index);

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
    /// Add a freeform group to this screen.
    /// <br/>The provided delegate will be used for unselector navigation.
    /// </summary>
    public UIFreeformGroup AddFreeformGroup(Func<UINode, ICursorState, UICommand, UIResult?>? unselectNav = null) =>
        new(this, EmptyNode.MakeUnselector(unselectNav));
    
    /// <summary>
    /// Create a <see cref="UIRenderExplicit"/> that queries for the HTML subtree named `name`.
    /// </summary>
    public UIRenderExplicit Q(string name) => new(this, name);

    /// <summary>
    /// Mark all nodes on this screen as destroyed.
    /// <br/>Does not affect HTML (call <see cref="DestroyScreen"/> instead to destroy HTML).
    /// Call this method when the menu containing this screen is being destroyed.
    /// </summary>
    public void MarkScreenDestroyed() {
        foreach (var r in Renderers)
            r.MarkViewsDestroyed();
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