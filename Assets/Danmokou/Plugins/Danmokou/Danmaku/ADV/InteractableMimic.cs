﻿using System;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Assertions;
using BagoumLib.Cancellation;
using BagoumLib.Culture;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using BagoumLib.Tasks;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Services;
using Danmokou.UI;
using Danmokou.UI.XML;
using Suzunoya.ADV;
using Suzunoya.Assertions;
using Suzunoya.ControlFlow;
using Suzunoya.Entities;
using SuzunoyaUnity;
using SuzunoyaUnity.Mimics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Danmokou.ADV {

public abstract record InteractableInfo {
    public abstract Sprite Icon { get; }
    public virtual bool UseWaveEffect => true;
    public abstract LString? Tooltip { get; init; }

    public record Dialogue(LString? Tooltip = null) : InteractableInfo {
        public override Sprite Icon => GameManagement.ADVReferences.talkToIcon;
    }
    
    public record DialogueObject(LString? Tooltip = null) : InteractableInfo {
        public override Sprite Icon => GameManagement.ADVReferences.talkToObjectIcon;
    }

    public record EvidenceTarget(LString? Tooltip = null) : InteractableInfo {
        public override Sprite Icon => GameManagement.ADVReferences.evidenceTargetIcon;
    }

    public record Map(bool Current, LString? Tooltip = null) : InteractableInfo {
        public override Sprite Icon => 
            Current ? GameManagement.ADVReferences.mapCurrentIcon : GameManagement.ADVReferences.mapNotCurrentIcon;
        public override bool UseWaveEffect => false;
    }
}

/// <summary>
/// A VN entity that can be clicked on to trigger something (generally a <see cref="BoundedContext{T}"/>).
/// <br/>By default, the interactable will only be active and clickable during the <see cref="ADVManager.State.Investigation"/> state,
///  but this can be changed via <see cref="InteractableStates"/>.
/// </summary>
public class Interactable : Rendered {
    public ADVManager Manager { get; set; } = null!;
    public IFreeformContainer? XMLContainer { get; set; } = null;
    public Func<UINode, UIResult?> OnClick { get; set; } = null!;
    public Evented<bool> Exhausted { get; set; } = new(false);
    public InteractableInfo Metadata { get; set; } = null!;
    public HoverAction? Hover { get; set; }
    public ADVManager.State[] InteractableStates { get; set; } = { ADVManager.State.Investigation };
    
    public abstract record HoverAction {
        public abstract IDisposable? Enter(Interactable i);

        public record VNOp(Func<VNOperation> Line) : HoverAction {
            public override IDisposable? Enter(Interactable i) {
                var cts = new Cancellable();
                var ct = new JointCancellee(cts, i.Manager.VNState.CToken);
                var t = i.Manager.TryExecuteVN(new BoundedContext<Unit>(i.Manager.VNState, "", async () => {
                    await Line().TaskWithCT(ct);
                    i.Manager.VNState.Run(WaitingUtils.Spin(WaitingUtils.GetCompletionAwaiter(out var t), ct));
                    await t;
                    return default;
                }) { Trivial = true }, true)?.ContinueWithSync(null);
                return t == null ? null : cts;
            }
        }
    }

    public override void Delete() {
        base.Delete();
        Visible.OnCompleted();
    }
}


public class InteractableMimic : RenderedMimic, IFixedXMLReceiver {
    public override Type[] CoreTypes => new[] {typeof(Interactable)};
    public override string SortingLayerFromPrefab => "";

    public FixedXMLHelper xml = null!;
    LString? IFixedXMLReceiver.Tooltip => entity.Metadata.Tooltip;
    
    private Interactable entity = null!;
    
    private readonly PushLerperF<float> offsetter = new(0.3f, BMath.LerpU);
    private readonly PushLerper<float> borderColor = new(0.2f, (a, b, t) => BMath.LerpU(a, b, cssDefaultEase(t)));
    private static readonly Easer cssDefaultEase = Bezier.CBezier(0.25f, 0.1f, 0.25f, 1f);
    private static readonly Easer bEase = Easers.CEOutBounce(0, 0.45f, 0.7f, 0.85f, 1f);
    private static readonly Func<float, float> bounce = t => 160 * (-1f + bEase(Mathf.Clamp01(BMath.Mod(2f, t) / 1.2f)));
    private static readonly Func<float, float> _wave = t => 20 * M.SinDeg(50 * t);
    private static readonly Func<float, float> _wave0 = t => 0;
    private Func<float, float> Wave => (entity.Metadata ?? throw new Exception("hello")).UseWaveEffect ? _wave : _wave0;
    private VisualElement? w = null!;

    private IDisposable? hoverActiveAction;

    public override void _Initialize(IEntity ent) => Initialize((ent as Interactable)!);

    
    private void Initialize(Interactable c) {
        base.Initialize(entity = c);
        xml.Receiver = this;
        if (c.XMLContainer is {} cont)
            cont.AddNodeDynamic(xml.MakeNode());
        offsetter.Push(Wave);
        borderColor.Push(0f);

        tokens.Add(c.ComputedTint.AddDisturbance(c.Exhausted.Map( 
            exh => exh ? new FColor(0.65f, 0.65f, 0.65f, 1f) : FColor.White)));
        bool isFirstState = true;
        var man = ServiceLocator.Find<ADVManager>();
        tokens.Add(c.Visible.AddDisturbance(man.ADVState.Map(s =>
            //If loading into a new map, the method is Waiting, but we want to still show the icons.
            // ReSharper disable once AccessToModifiedClosure
            c.InteractableStates.Contains(s) ||
            (s == ADVManager.State.Waiting && isFirstState && man.VNState.Contexts.Count == 0 && 
             !man.ExecAdv!.MapStates.CurrentMapState.HasEntryVN))));
        isFirstState = false;
    }

    void IFixedXMLReceiver.OnBuilt(UINode _n, IFixedXMLObject cfg) {
        //When the user hovers over the node, it should "bounce" or "wave". 
        // However, we don't want the root EmptyNode to change position, since that makes keyboard-based
        // movement unstable. Thus, we create a VE inside the empty node, which is `wb`.
        //`wb` has a border. Since Unity UXML has box-sizing: border-box, we don't want to use a border together with
        // background-image. Thus, to render the background, we create another VE within `wb`, which is `w`.
        //In this setup, the root emptynode has a size of XMLSize, `wb` has a size of XMLSize+2*borderWidth,
        // and `w` has a size of of XMLSize. (The border appears outside of the dimensions of the root emptynode.)
        if (_n is not EmptyNode n)
            throw new Exception("Interactable mimic must be attached to empty node");
        var wb = new VisualElement().ConfigureAbsolute().ConfigureLeftTopListeners(
            n.CreateCenterOffsetChildX(new ConstantObservable<float>(0)),
            n.CreateCenterOffsetChildY(this.offsetter));
        n.HTML.Add(wb);
        borderColor.Subscribe(a => wb.SetBorder(
            new Color(.812f, .545f, 0, a)
            //new Color(0.6f, 0f, 0.5f, a)
            , 10));
        wb.Add(w = new VisualElement());
        wb.SetRecursivePickingMode(PickingMode.Ignore); //only the root empty node is pickable
        w.ConfigureWidthHeightListeners(cfg.Width, cfg.Height);
        w.style.backgroundImage = new(entity.Metadata.Icon);
        w.style.unityBackgroundImageTintColor = entity.ComputedTint.Value._();

        //TransitionHelpers.Apply(t => 40 * M.SinDeg(70 * t), float.PositiveInfinity, x => w.style.top = x)
        //    .Run(this, CoroutineOptions.Droppable);

        /*
    w.transform.ScaleTo(1.02f, 0.1f, Easers.EOutSine, cT)
        .Then(() => NodeHTML.transform.ScaleTo(1f, 0.13f, cT: cT))
        .Run(Controller, new CoroutineOptions(true))*/
    }

    UIResult? IFixedXMLReceiver.Navigate(UINode n, ICursorState cs, UICommand req) {
        if (req == UICommand.Confirm) {
            if (entity.InteractableStates.Contains(ServiceLocator.Find<ADVManager>().ADVState.Value)) {
                //OnLeave(n); //Implicitly called through UpdatePassthrough > MoveCursorAwayFromNode
                return entity.OnClick(n) ?? new UIResult.StayOnNode();
            } else
                return new UIResult.StayOnNode(UIResult.StayOnNodeType.Silent);
        }
        return null;
    }
    
    protected override void DoUpdate(float dT) {
        offsetter.Update(dT);
        borderColor.Update(dT);
        base.DoUpdate(dT);
    }

    void IFixedXMLReceiver.OnEnter(UINode n, ICursorState cs) {
        hoverActiveAction?.Dispose();
        hoverActiveAction = entity.Hover?.Enter(entity);
        offsetter.Push(bounce);
        borderColor.Push(1f);
    }

    void IFixedXMLReceiver.OnLeave(UINode n, ICursorState cs) {
        hoverActiveAction?.Dispose();
        hoverActiveAction = null;
        offsetter.Push(Wave);
        borderColor.Push(0);
    }
    

    protected override void SetSortingLayer(int layer) { }

    protected override void SetSortingID(int id) { }

    protected override void SetVisible(bool visible) {
        xml.XML.IsVisible.Value = visible;
    }

    protected override void SetTint(Color c) {
        if (w != null)
            w.style.unityBackgroundImageTintColor = c;
    }
}

public record InteractableAssertion(ADVManager Manager, Func<UINode, UIResult?> OnClick, string ID) : 
    EntityAssertion<Interactable>(Manager.VNState, ID), IAssertion<InteractableAssertion> {
    public InteractableAssertion(ADVManager manager, Action onClick, string id) : this(manager, _ => {
        onClick();
        return null;
    }, id) { }
    
    public IFreeformContainer? XMLContainer { get; set; } = null;
    public InteractableInfo Info { get; set; } = new InteractableInfo.Dialogue();
    public bool Exhausted { get; init; }
    public Interactable.HoverAction? Hover { get; init; }
    public ADVManager.State[] InteractableStates { get; init; } = { ADVManager.State.Investigation };

    protected override void Bind(Interactable ie) {
        base.Bind(ie);
        ie.Exhausted.OnNext(Exhausted);
        ie.Manager = Manager;
        ie.XMLContainer = XMLContainer;
        ie.OnClick = OnClick;
        ie.Hover = Hover;
        ie.Metadata = Info;
        ie.InteractableStates = InteractableStates;
    }

    public InteractableAssertion AsObject() {
        Info = new InteractableInfo.DialogueObject();
        return this;
    }
    
    Task IAssertion.Inherit(IAssertion prev) => AssertionHelpers.Inherit<InteractableAssertion>(prev, this);

    public Task _Inherit(InteractableAssertion prev) => base._Inherit(prev);
}

public record InteractableBCtxAssertion : InteractableAssertion, IAssertion<InteractableBCtxAssertion> {
    public InteractableBCtxAssertion(ADVManager Manager, BoundedContext<Unit> OnClick) : 
        base(Manager, () => {
            _ = Manager.ExecuteVN(OnClick).ContinueWithSync(null);
        }, OnClick.ID) {
        //NB: by default, BCTX reuse local data when being repeatedly run;
        // this means that if a BCTX is run to completion once, then run again but interrupted,
        // the "Result" will be persisted from the first execution,
        // and the BCTX will be considered completed.
        Exhausted = OnClick.IsCompletedInContexts();
    }

    Task IAssertion.Inherit(IAssertion prev) => AssertionHelpers.Inherit<InteractableBCtxAssertion>(prev, this);

    public Task _Inherit(InteractableBCtxAssertion prev) => base._Inherit(prev);
}

public record InteractableEvidenceTargetA<E, T> : InteractableAssertion, IAssertion<InteractableEvidenceTargetA<E, T>> 
    where E: class where T : IEvidenceTarget {
    
    public InteractableEvidenceTargetA(ADVManager Manager, EvidenceTargetProxy<E, T> Handler, T Target, string? ID = null) : 
        base(Manager, _ => Handler.Present(Target), ID ?? $"{Target}") {
        Info = new InteractableInfo.EvidenceTarget(Target.Tooltip);
        InteractableStates = ADVManager.AllStates;
        //currently don't have any good way to implement Exhausted 
        // since PresentToTarget functionality may change depending on Data
        //We could do a minimal setup where we locally store (Evidence, Target) pairs, so they get cleared
        // when the data (and assertions) change
    }

    Task IAssertion.Inherit(IAssertion prev) => AssertionHelpers.Inherit<InteractableEvidenceTargetA<E, T>>(prev, this);

    public Task _Inherit(InteractableEvidenceTargetA<E, T> prev) => base._Inherit(prev);
}


}