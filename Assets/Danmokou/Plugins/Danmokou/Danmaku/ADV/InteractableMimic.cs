using System;
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
    public IFixedXMLObjectContainer? XMLContainer { get; set; } = null;
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
                }), true)?.ContinueWithSync(null);
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
    public LString? Tooltip => entity.Metadata.Tooltip;
    
    private Interactable entity = null!;
    
    private readonly PushLerperF<float> offsetter = new(0.3f, M.LerpU);
    private readonly PushLerper<float> borderColor = new(0.2f, (a, b, t) => M.LerpU(a, b, cssDefaultEase(t)));
    private static readonly Easer cssDefaultEase = Bezier.CBezier(0.25f, 0.1f, 0.25f, 1f);
    private static readonly Easer bEase = Easers.CEOutBounce(0, 0.45f, 0.7f, 0.85f, 1f);
    private readonly Func<float, float> bounce = t => 160 * (-1f + bEase(Mathf.Clamp01(BMath.Mod(2f, t) / 1.2f)));
    private readonly Func<float, float> _wave = t => 20 * M.SinDeg(50 * t);
    private readonly Func<float, float> _wave0 = t => 0;
    private Func<float, float> Wave => (entity.Metadata ?? throw new Exception("hello")).UseWaveEffect ? _wave : _wave0;
    private VisualElement? w = null!;

    private IDisposable? hoverActiveAction;

    public override void _Initialize(IEntity ent) => Initialize((ent as Interactable)!);

    
    private void Initialize(Interactable c) {
        base.Initialize(entity = c);
        xml.Container = c.XMLContainer;
        offsetter.Push(Wave);
        borderColor.Push(0f);

        tokens.Add(c.ComputedTint.AddDisturbance(c.Exhausted.Map( 
            exh => exh ? new FColor(0.65f, 0.65f, 0.65f, 1f) : FColor.White)));
        Listen(c.ComputedLocation, _ => xml.UpdatedLocations());
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

    public void OnBuilt(EmptyNode n) {
        var wb = new VisualElement().ConfigureAbsolute().ConfigureEmpty(false).ConfigureLeftTopListeners(
            n.CreateCenterOffsetChildX(new ConstantObservable<float>(0)),
            n.CreateCenterOffsetChildY(this.offsetter));
        n.HTML.Add(wb);
        borderColor.Subscribe(a => wb.SetBorder(
            new Color(.812f, .545f, 0, a)
            //new Color(0.6f, 0f, 0.5f, a)
            , 10));
        w = new VisualElement().ConfigureEmpty(false);
        wb.Add(w);
        w.style.width = 160;
        w.style.height = 160;
        w.style.backgroundImage = new(entity.Metadata.Icon);
        w.style.unityBackgroundImageTintColor = entity.ComputedTint.Value._();

        //TransitionHelpers.Apply(t => 40 * M.SinDeg(70 * t), float.PositiveInfinity, x => w.style.top = x)
        //    .Run(this, CoroutineOptions.Droppable);

        /*
    w.transform.ScaleTo(1.02f, 0.1f, Easers.EOutSine, cT)
        .Then(() => NodeHTML.transform.ScaleTo(1f, 0.13f, cT: cT))
        .Run(Controller, new CoroutineOptions(true))*/
    }

    public UIResult OnConfirm(UINode n) {
        if (entity.InteractableStates.Contains(ServiceLocator.Find<ADVManager>().ADVState.Value)) {
            //OnLeave(n); //Implicitly called through UpdatePassthrough > MoveCursorAwayFromNode
            return entity.OnClick(n) ?? new UIResult.StayOnNode();
        } else
            return new UIResult.StayOnNode(UIResult.StayOnNodeType.Silent);
    }
    
    protected override void DoUpdate(float dT) {
        offsetter.Update(dT);
        borderColor.Update(dT);
        base.DoUpdate(dT);
    }

    public void OnEnter(UINode n) {
        hoverActiveAction?.Dispose();
        hoverActiveAction = entity.Hover?.Enter(entity);
        offsetter.Push(bounce);
        borderColor.Push(1f);
    }

    public void OnLeave(UINode n) {
        hoverActiveAction?.Dispose();
        hoverActiveAction = null;
        offsetter.Push(Wave);
        borderColor.Push(0);
    }

    public void OnPointerDown(UINode n, PointerDownEvent ev) {}

    public void OnPointerUp(UINode n, PointerUpEvent ev) {}
    

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
    
    public IFixedXMLObjectContainer? XMLContainer { get; set; } = null;
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