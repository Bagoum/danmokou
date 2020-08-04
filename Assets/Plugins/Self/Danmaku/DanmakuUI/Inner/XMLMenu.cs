using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Danmaku;
using Danmaku.DanmakuUI;
using JetBrains.Annotations;
using UnityEngine.UIElements;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using static GameManagement;
using static Danmaku.MainMenu;

/// <summary>
/// Abstract class for all in-game UI based on UIBuilder.
/// </summary>
[Preserve]
public abstract class XMLMenu : MonoBehaviour {
    public VisualElement UI { get; private set; }
    [CanBeNull] protected VisualElement UITop;

    protected virtual string UITopID => "Pause";
    protected virtual string ScreenContainerID => "UIContainer";

    protected virtual UIScreen[] Screens => new[] { MainScreen };
    protected UIScreen MainScreen { get; set; }
    protected bool MenuActive = true;
    [CanBeNull] protected virtual string HeaderOverride => null;
    
    protected abstract Dictionary<Type, VisualTreeAsset> TypeMap { get; }

    [CanBeNull] protected UINode Current;

    [CanBeNull] public GameObject MainScreenOnlyObjects;

    protected virtual void ResetCurrentNode() {
        Current = MainScreen.StartingNode;
    }

    public SFXConfig upDownSound;
    public SFXConfig leftRightSound;
    public SFXConfig confirmSound;
    public SFXConfig failureSound;
    public SFXConfig backSound;
    protected virtual void Awake() {
        if (!Application.isPlaying) return;
        Current = MainScreen.StartingNode;
    }

    protected virtual void Start() {
        UI = GetComponent<UIDocument>().rootVisualElement;
        Rebind();
    }

    protected virtual IEnumerable<Object> Rebind() {
        UITop = UI.Q(UITopID);
        var container = UI.Q(ScreenContainerID);
        foreach (var s in Screens) container.Add(s.Build(TypeMap));
        Redraw();
        if (HeaderOverride != null) UI.Q<Label>("Header").text = HeaderOverride;
        return null;
    }

    protected void Redraw() {
        foreach (var screen in Screens) {
            if (Current?.screen == screen) {
                screen.Bound.style.display = DisplayStyle.Flex;
            } else {
                screen.Bound.style.display = DisplayStyle.None;
                screen.ResetNodes();
            }
        }
        if (MainScreenOnlyObjects != null) {
            MainScreenOnlyObjects.SetActive(Current?.screen == MainScreen);
        }
        if (Current != null) {
            Current.AssignStatesFromSelected();
            Current.ScrollTo();
        }
    }
    protected void SimulateSelectIndex(int index) {
        Current = Current.siblings[index].Confirm().Item2;
        Redraw();
    }

    public new void Update() {
        if (!Application.isPlaying) return;
        if (Current != null && MenuActive) {
            var from_current = Current;
            if (InputManager.UILeft.Active) {
                SFXService.Request(leftRightSound);
                Current = Current.Left();
            } else if (InputManager.UIRight.Active) {
                SFXService.Request(leftRightSound);
                Current = Current.Right();
            } else if (InputManager.UIUp.Active) {
                SFXService.Request(upDownSound);
                Current = Current.Up();
            } else if (InputManager.UIDown.Active) {
                SFXService.Request(upDownSound);
                Current = Current.Down();
            } else if (InputManager.UIConfirm.Active) {
                var (succ, nxt) = Current.Confirm();
                Current = nxt;
                if (succ) SFXService.Request(confirmSound);
                else SFXService.Request(failureSound);
            } else if (InputManager.UIBack.Active) {
                SFXService.Request(backSound);
                Current = Current.Back();
            }
            if (from_current != Current) {
                Redraw();
            }
        }

    }

}