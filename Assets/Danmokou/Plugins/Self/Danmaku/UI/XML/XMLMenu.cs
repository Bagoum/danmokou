using System;
using System.Collections;
using System.Collections.Generic;
using DMK.Behavior;
using DMK.Core;
using DMK.DMath;
using DMK.Scriptables;
using DMK.Services;
using JetBrains.Annotations;
using UnityEngine.UIElements;
using UnityEngine;
using UnityEngine.Scripting;
using Object = UnityEngine.Object;
using static DMK.Core.GameManagement;

namespace DMK.UI.XML {
/// <summary>
/// Abstract class for all in-game UI based on UIBuilder.
/// </summary>
[Preserve]
public abstract class XMLMenu : RegularUpdater {
    public UIBuilderRenderer uiRenderer;
    [CanBeNull] protected virtual List<int> ReturnTo { get; set; }
    [CanBeNull] private List<int> tentativeReturnTo;

    protected void TentativeCache(List<int> indices) {
        tentativeReturnTo = indices;
    }

    protected void ConfirmCache() {
        if (tentativeReturnTo != null)
            Log.Unity($"Caching menu position with indices {string.Join(", ", tentativeReturnTo)}");
        ReturnTo = tentativeReturnTo;
    }

    public VisualElement UI { get; private set; }
    [CanBeNull] protected VisualElement UITop;

    protected virtual string UITopID => "Pause";
    protected virtual string ScreenContainerID => "UIContainer";

    protected enum ScreenTransition {
        SWIPE,
        NONE
    }

    protected virtual ScreenTransition transitionMethod => ScreenTransition.SWIPE;

    protected virtual IEnumerable<UIScreen> Screens => new[] {MainScreen};
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
        Current = MainScreen?.StartingNode;
    }

    protected virtual void Start() {
        UI = GetComponent<UIDocument>().rootVisualElement;
        Rebind();
    }

    protected virtual IEnumerable<Object> Rebind() {
        UITop = UI.Q(UITopID);
        var container = UI.Q(ScreenContainerID);
        foreach (var s in Screens) container.Add(s.Build(TypeMap));
        if (HeaderOverride != null) UI.Q<Label>("Header").text = HeaderOverride;

        if (ReturnTo != null) {
            for (int ii = 0; ii < ReturnTo.Count; ++ii) {
                var prev = Current;
                if (ii > 0) {
                    if (Current.children.Length > 0) Current = Current.children[ReturnTo[ii]];
                    else Current = Current.Confirm().Item2.Siblings[ReturnTo[ii]];
                } else Current = Current.Siblings[ReturnTo[ii]];
                Current.OnVisit(prev);
            }
        }
        Redraw();
        ReturnTo = null;
        return null;
    }

    protected virtual void Redraw() {
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

    public override bool UpdateDuringPause => true;

    private bool isTransitioning = false;

    public override void RegularUpdate() {
        if (!Application.isPlaying || !ETime.FirstUpdateForScreen) return;
        if (Current != null && MenuActive && !isTransitioning) {
            bool tried_change = true;
            bool allowsfx = true;
            var last = Current;
            int sentry = 0;
            do {
                var custom = Current.CustomEventHandling();
                if (custom != null) {
                    SFXService.Request(leftRightSound); //add another sound
                    Current = custom;
                } else if (InputManager.UILeft.Active) {
                    if (allowsfx) SFXService.Request(leftRightSound);
                    Current = Current.Left();
                } else if (InputManager.UIRight.Active) {
                    if (allowsfx) SFXService.Request(leftRightSound);
                    Current = Current.Right();
                } else if (InputManager.UIUp.Active) {
                    if (allowsfx) SFXService.Request(upDownSound);
                    Current = Current.Up();
                } else if (InputManager.UIDown.Active) {
                    if (allowsfx) SFXService.Request(upDownSound);
                    Current = Current.Down();
                } else if (InputManager.UIConfirm.Active) {
                    var (succ, nxt) = Current.Confirm_DontNest();
                    if (succ) HandleTransition(Current, nxt, false);
                    if (allowsfx) SFXService.Request(succ ? confirmSound : failureSound);
                } else if (InputManager.UIBack.Active) {
                    if (allowsfx) SFXService.Request(backSound);
                    HandleTransition(Current, Current.Back(), true);
                } else tried_change = false;
                allowsfx = false;
                if (++sentry > 20) throw new Exception("There is a loop in the XML menu.");
            } while (Current?.Passthrough ?? false);
            if (tried_change) {
                OnChangeEffects(last);
                Redraw();
            }
        }
    }

    private void OnChangeEffects(UINode last) {
        if (last != Current) {
            last.OnLeave(Current);
            Current?.OnVisit(last);
        }
        Redraw();
    }

    private float swipeTime = 0.3f;

    private void HandleTransition(UINode prev, [CanBeNull] UINode next, bool backwards) {
        if (prev.screen == next?.screen || next == null) {
            Current = next;
            OnChangeEffects(prev);
        } else {
            prev.screen?.RunPreExit();
            isTransitioning = true;
            void GoToNested() {
                if (backwards) {
                    Current = prev.screen.GoBack();
                } else {
                    Current = prev.screen.GoToNested(prev, next);
                }
                OnChangeEffects(prev);
            }
            if (transitionMethod == ScreenTransition.SWIPE) {
                uiRenderer.Slide(null, GetRandomSlideEndpoint(), swipeTime, DMath.M.EInSine, s => {
                    if (s) {
                        GoToNested();
                        uiRenderer.Slide(GetRandomSlideEndpoint(), Vector2.zero, swipeTime, DMath.M.EOutSine, s2 => {
                            if (s2) isTransitioning = false;
                        });
                    }
                });
            } else GoToNested();
        }
    }

    private Vector2[] slideEndpoints => new[] {
        new Vector2(-MainCamera.ScreenWidth, 0),
        new Vector2(MainCamera.ScreenWidth, 0),
        new Vector2(0, -MainCamera.ScreenHeight),
        new Vector2(0, MainCamera.ScreenHeight)
    };

    private Vector2 GetRandomSlideEndpoint() {
        return RNG.RandSelectOffFrame(slideEndpoints);
    }
}
}