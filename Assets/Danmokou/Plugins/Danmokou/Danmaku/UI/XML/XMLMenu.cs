using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Events;
using BagoumLib.Tasks;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Scriptables;
using Danmokou.Services;
using JetBrains.Annotations;
using UnityEngine.UIElements;
using UnityEngine;
using UnityEngine.Scripting;
using Object = UnityEngine.Object;
using static Danmokou.Services.GameManagement;

namespace Danmokou.UI.XML {
public enum QueuedEvent {
    Goto,
    Confirm,
    Left,
    Right
}
/// <summary>
/// Abstract class for all in-game UI based on UIBuilder.
/// </summary>
[Preserve]
public abstract class XMLMenu : CoroutineRegularUpdater {
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
            Logs.Log($"Caching menu position with indices " +
                      $"{string.Join(", ", tentativeReturnTo.Select(x => x.ToString()))}");
        ReturnTo = tentativeReturnTo;
    }

    public VisualElement UI { get; private set; } = null!;
    protected virtual string ScreenContainerID => "UIContainer";

    protected enum ScreenTransition {
        SWIPE,
        NONE
    }

    protected virtual ScreenTransition transitionMethod => ScreenTransition.SWIPE;

    protected virtual IEnumerable<UIScreen> Screens => new[] {MainScreen};
    protected UIScreen MainScreen { get; set; } = null!;
    protected bool MenuActive { get; set; }= true;

    protected virtual Dictionary<Type, VisualTreeAsset> TypeMap => References.uxmlDefaults.TypeMap;

    public UINode? Current { get; protected set; } = null;

    public GameObject? MainScreenOnlyObjects;

    //Fields for event-based changes 
    //Not sure if I want to generalize these to properly event-based...
    public (UINode src, QueuedEvent ev)? QueuedEvent { get; set; }

    protected virtual void ResetCurrentNode() {
        Current = MainScreen.StartingNode;
    }

    public SFXConfig? upDownSound;
    public SFXConfig? leftRightSound;
    public SFXConfig? confirmSound;
    public SFXConfig? failureSound;
    public SFXConfig? backSound;

    public override void FirstFrame() {
        Current = MainScreen.StartingNode!;
        UI = GetComponent<UIDocument>().rootVisualElement;
        Rebind();
    }

    protected virtual void Rebind() {
        var container = UI.Q(ScreenContainerID);
        foreach (var s in Screens) {
            if (s != null)
                container.Add(s.Build(TypeMap));
        }

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
                    Current.OnVisit(prev, false);
                } else if (inst.type == CacheInstruction.InstrType.GOTO_CHILD) {
                    Current = Current.children[inst.instrVal];
                    Current.OnVisit(prev, false);
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
            RunDroppableRIEnumerator(scrollToCurrent());
        }
    }

    //Workaround for limitation that cannot ScrollTo to objects that have just been constructed or made visible
    private IEnumerator scrollToCurrent() {
        yield return new WaitForEndOfFrame();
        Current?.ScrollTo();
    }

    public override EngineState UpdateDuring => EngineState.MENU_PAUSE;

    public DisturbedAnd UpdatesEnabled { get; } = new DisturbedAnd();

    protected bool RegularUpdateGuard => Application.isPlaying && ETime.FirstUpdateForScreen && UpdatesEnabled;
    public override void RegularUpdate() {
        base.RegularUpdate();
        if (RegularUpdateGuard && Current != null && MenuActive) {
            bool tried_change = true;
            bool allowsfx = true;
            var last = Current;
            int sentry = 0;
            do {
                var custom = Current.CustomEventHandling();
                var qeIsCurr = QueuedEvent?.src == Current;
                if (custom != null) {
                    ServiceLocator.SFXService.Request(leftRightSound); //add another sound
                    Current = custom;
                } else if (InputManager.UILeft.Active ||  (qeIsCurr && QueuedEvent?.ev == XML.QueuedEvent.Left)) {
                    if (allowsfx) 
                        ServiceLocator.SFXService.Request(leftRightSound);
                    Current = Current.Left();
                } else if (InputManager.UIRight.Active || (qeIsCurr && QueuedEvent?.ev == XML.QueuedEvent.Right)) {
                    if (allowsfx) 
                        ServiceLocator.SFXService.Request(leftRightSound);
                    Current = Current.Right();
                } else if (InputManager.UIUp.Active) {
                    if (allowsfx) 
                        ServiceLocator.SFXService.Request(upDownSound);
                    Current = Current.Up();
                } else if (InputManager.UIDown.Active) {
                    if (allowsfx)
                        ServiceLocator.SFXService.Request(upDownSound);
                    Current = Current.Down();
                } else if (QueuedEvent.Try(out var qe) && qe.ev == XML.QueuedEvent.Goto 
                                                       && qe.src != Current && Current.Siblings.Contains(qe.src)) {
                    if (allowsfx)
                        ServiceLocator.SFXService.Request(upDownSound);
                    Current = qe.src;
                } else if (InputManager.UIConfirm.Active || (qeIsCurr && QueuedEvent?.ev == XML.QueuedEvent.Confirm)) {
                    var (succ, nxt) = Current.Confirm_DontNest();
                    if (succ) 
                        HandleTransition(Current, nxt, false);
                    if (allowsfx) 
                        ServiceLocator.SFXService.Request(succ ? confirmSound : failureSound);
                    if (Current?.Passthrough == true) Current = Current.Parent;
                } else if (InputManager.UIBack.Active) {
                    if (allowsfx) 
                        ServiceLocator.SFXService.Request(backSound);
                    HandleTransition(Current, Current.Back(), true);
                } else tried_change = false;
                allowsfx = false;
                if (++sentry > 100) throw new Exception("There is a loop in the XML menu.");
            } while (Current?.Passthrough ?? false);
            if (tried_change) {
                //TODO animations are extremely slow when navigating with mouse, notsure why.
                //Might be due to scaling animations causing recalculation of moise containment?
                OnChangeEffects(last, QueuedEvent == null);
            }
        }
        if (Application.isPlaying && ETime.FirstUpdateForScreen) {
            QueuedEvent = null;
        }

    }

    private void OnChangeEffects(UINode last, bool animate) {
        if (last != Current) {
            last.OnLeave(Current);
            Current?.OnVisit(last, animate);
        }
        Redraw();
    }

    private float swipeTime = 0.3f;

    private void HandleTransition(UINode prev, UINode? next, bool backwards) {
        if (prev.screen == next?.screen || next == null) {
            Current = next;
            OnChangeEffects(prev, true);
        } else {
            prev.screen.RunPreExit();
            var token = UpdatesEnabled.AddConst(false);
            next.screen.RunPreEnter();
            void GoToNested() {
                if (backwards) {
                    Current = prev.screen.GoBack();
                } else {
                    Current = prev.screen.GoToNested(prev, next);
                }
                OnChangeEffects(prev, false);
            }
            if (transitionMethod == ScreenTransition.SWIPE) {
                async Task Transition() {
                    var c = await uiRenderer.Slide(null, GetRandomSlideEndpoint(), swipeTime, M.EInSine);
                    if (c != Completion.Standard) return;
                    GoToNested();
                    c = await uiRenderer.Slide(GetRandomSlideEndpoint(), Vector2.zero, swipeTime, M.EOutSine);
                    if (c != Completion.Standard) return;
                    next.screen.RunPostEnter();
                    token.Dispose();
                }
                _ = Transition().ContinueWithSync(() => { });
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