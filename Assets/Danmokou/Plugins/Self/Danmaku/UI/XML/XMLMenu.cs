using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public readonly struct CacheInstruction {
        public enum InstrType {
            GOTO_CHILD,
            GOTO_SIBLING,
            GOTO_OPTION,
            CONFIRM
        }

        public readonly InstrType type;
        public readonly int instrVal;

        private CacheInstruction(InstrType type, int instrVal) {
            this.type = type;
            this.instrVal = instrVal;
        }

        public override string ToString() => $"{type}:{instrVal}";

        public static CacheInstruction ToChild(int idx) => new CacheInstruction(InstrType.GOTO_CHILD, idx);
        public static CacheInstruction ToSibling(int idx) => new CacheInstruction(InstrType.GOTO_SIBLING, idx);
        public static CacheInstruction ToOption(int idx) => new CacheInstruction(InstrType.GOTO_OPTION, idx);
        public static CacheInstruction Confirm => new CacheInstruction(InstrType.CONFIRM, 0);
    }
    public UIBuilderRenderer uiRenderer = null!;
    //The reason for this virtual structure is because implementers (eg. XMLMainMenuCampaign)
    // need to store the list *statically*, since the specific menu object will be deleted on scene change.
    protected virtual List<CacheInstruction>? ReturnTo { get; set; }
    private List<CacheInstruction>? tentativeReturnTo;

    protected void TentativeCache(List<CacheInstruction> indices) {
        tentativeReturnTo = indices;
    }

    protected void ConfirmCache() {
        if (tentativeReturnTo != null)
            Log.Unity($"Caching menu position with indices " +
                      $"{string.Join(", ", tentativeReturnTo.Select(x => x.ToString()))}");
        ReturnTo = tentativeReturnTo;
    }

    public VisualElement UI { get; private set; } = null!;
    protected VisualElement? UITop;

    protected virtual string UITopID => "Pause";
    protected virtual string ScreenContainerID => "UIContainer";

    protected enum ScreenTransition {
        SWIPE,
        NONE
    }

    protected virtual ScreenTransition transitionMethod => ScreenTransition.SWIPE;

    protected virtual IEnumerable<UIScreen> Screens => new[] {MainScreen};
    protected UIScreen MainScreen { get; set; } = null!;
    protected bool MenuActive = true;
    protected virtual string? HeaderOverride => null;

    protected virtual Dictionary<Type, VisualTreeAsset> TypeMap => References.uxmlDefaults.TypeMap;

    protected UINode? Current = null;

    public GameObject? MainScreenOnlyObjects;

    protected virtual void ResetCurrentNode() {
        Current = MainScreen.StartingNode;
    }

    public SFXConfig? upDownSound;
    public SFXConfig? leftRightSound;
    public SFXConfig? confirmSound;
    public SFXConfig? failureSound;
    public SFXConfig? backSound;

    protected virtual void Awake() {
        if (!Application.isPlaying) return;
        Current = MainScreen.StartingNode!;
    }

    protected virtual void Start() {
        UI = GetComponent<UIDocument>().rootVisualElement;
        Rebind();
    }

    protected virtual void Rebind() {
        UITop = UI.Q(UITopID);
        var container = UI.Q(ScreenContainerID);
        foreach (var s in Screens) {
            if (s != null)
                container.Add(s.Build(TypeMap));
        }
        if (HeaderOverride != null) UI.Q<Label>("Header").text = HeaderOverride;

        if (ReturnTo != null) {
            if (Current == null)
                throw new Exception("ReturnTo exists, but Current is null");
            for (int ii = 0; ii < ReturnTo.Count; ++ii) {
                var prev = Current;
                var inst = ReturnTo[ii];
                if (inst.type == CacheInstruction.InstrType.CONFIRM) {
                    Current = Current.Confirm().target!;
                    if (Current.screen != prev.screen) {
                        //The exit/enter events are handled by Confirm internals
                        prev.screen.RunPreExit();
                        Current.screen.RunPreEnter();
                        Current.screen.RunPostEnter();
                    }
                } else if (inst.type == CacheInstruction.InstrType.GOTO_OPTION) {
                    if (Current is IOptionNodeLR opt) {
                        opt.Index = inst.instrVal;
                    } else
                        throw new Exception("Couldn't rebuild menu position: node is not an option");
                } else if (inst.type == CacheInstruction.InstrType.GOTO_SIBLING) {
                    Current = Current.Siblings[inst.instrVal];
                    Current.OnVisit(prev);
                } else if (inst.type == CacheInstruction.InstrType.GOTO_CHILD) {
                    Current = Current.children[inst.instrVal];
                    Current.OnVisit(prev);
                } else
                    throw new Exception($"Couldn't resolve instruction {inst.type}");
            }
        }
        Redraw();
        ReturnTo = null;
    }

    protected virtual void Redraw() {
        foreach (var screen in Screens) {
            if (Current?.screen == screen) {
                screen.Bound.style.display = DisplayStyle.Flex;
            } else {
                screen.Bound.style.display = DisplayStyle.None;
                screen.ResetNodeProgress();
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
                    DependencyInjection.SFXService.Request(leftRightSound); //add another sound
                    Current = custom;
                } else if (InputManager.UILeft.Active) {
                    if (allowsfx) 
                        DependencyInjection.SFXService.Request(leftRightSound);
                    Current = Current.Left();
                } else if (InputManager.UIRight.Active) {
                    if (allowsfx) 
                        DependencyInjection.SFXService.Request(leftRightSound);
                    Current = Current.Right();
                } else if (InputManager.UIUp.Active) {
                    if (allowsfx) 
                        DependencyInjection.SFXService.Request(upDownSound);
                    Current = Current.Up();
                } else if (InputManager.UIDown.Active) {
                    if (allowsfx) 
                        DependencyInjection.SFXService.Request(upDownSound);
                    Current = Current.Down();
                } else if (InputManager.UIConfirm.Active) {
                    var (succ, nxt) = Current.Confirm_DontNest();
                    if (succ) 
                        HandleTransition(Current, nxt, false);
                    if (allowsfx) 
                        DependencyInjection.SFXService.Request(succ ? confirmSound : failureSound);
                    if (Current?.Passthrough == true) Current = Current.Parent;
                } else if (InputManager.UIBack.Active) {
                    if (allowsfx) 
                        DependencyInjection.SFXService.Request(backSound);
                    HandleTransition(Current, Current.Back(), true);
                } else tried_change = false;
                allowsfx = false;
                if (++sentry > 100) throw new Exception("There is a loop in the XML menu.");
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

    private void HandleTransition(UINode prev, UINode? next, bool backwards) {
        if (prev.screen == next?.screen || next == null) {
            Current = next;
            OnChangeEffects(prev);
        } else {
            prev.screen.RunPreExit();
            isTransitioning = true;
            next.screen.RunPreEnter();
            void GoToNested() {
                if (backwards) {
                    Current = prev.screen.GoBack();
                } else {
                    Current = prev.screen.GoToNested(prev, next);
                }
                OnChangeEffects(prev);
            }
            if (transitionMethod == ScreenTransition.SWIPE) {
                uiRenderer.Slide(null, GetRandomSlideEndpoint(), swipeTime, M.EInSine, s => {
                    if (s) {
                        GoToNested();
                        uiRenderer.Slide(GetRandomSlideEndpoint(), Vector2.zero, swipeTime, M.EOutSine, s2 => {
                            if (s2) {
                                next.screen.RunPostEnter();
                                isTransitioning = false;
                            }
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