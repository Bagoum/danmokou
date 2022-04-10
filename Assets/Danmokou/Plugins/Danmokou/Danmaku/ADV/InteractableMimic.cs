using System;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib.Assertions;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using BagoumLib.Tasks;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.UI;
using Danmokou.UI.XML;
using Suzunoya.Assertions;
using Suzunoya.ControlFlow;
using Suzunoya.Entities;
using SuzunoyaUnity;
using SuzunoyaUnity.Mimics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Danmokou.ADV {

/// <summary>
/// A VN entity that can be clicked on to trigger something (generally a <see cref="BoundedContext{T}"/>).
/// </summary>
public class Interactable : Rendered {
    public ADVManager Manager { get; set; } = null!;
    public BoundedContext<Unit> OnClick { get; set; } = null!;
    public Evented<bool> Exhausted { get; set; } = new(false);
    
    public HoverAction? Hover { get; set; }
    
    public abstract record HoverAction {
        public abstract IDisposable Enter(Interactable i);

        public record VNOp(Func<VNOperation> Line) : HoverAction {
            public override IDisposable Enter(Interactable i) {
                var ct = new Cancellable();
                _ = i.Manager.ExecuteVN(new BoundedContext<Unit>(i.Manager.VNState, "", async () => {
                    await Line().TaskWithCT(ct);
                    i.Manager.VNState.Run(WaitingUtils.Spin(WaitingUtils.GetCompletionAwaiter(out var t), ct));
                    await t;
                    return default;
                }), ADVManager.State.Investigation).ContinueWithSync(null);
                return ct;
            }
        }
    }
}


public class InteractableMimic : RenderedMimic, IFixedXMLReceiver {
    public override Type[] CoreTypes => new[] {typeof(Interactable)};
    public override string SortingLayerFromPrefab => "";

    public FixedXMLHelper xml = null!;
    
    private Interactable entity = null!;
    
    private PushLerperF<float> offsetter = new(0.3f, Mathf.LerpUnclamped);
    private static readonly Easer b = Easers.CEOutBounce(0, 0.45f, 0.7f, 0.85f, 1f);
    private Func<float, float> bounce = t => 160 * (-1f + b(Mathf.Clamp01(M.Mod(2f, t) / 1.2f)));
    private Func<float, float> wave = t => 20 * M.SinDeg(50 * t);
    private VisualElement? w = null!;

    private IDisposable? hoverActiveAction;

    public override void _Initialize(IEntity ent) => Initialize((ent as Interactable)!);

    
    private void Initialize(Interactable c) {
        base.Initialize(entity = c);
        offsetter.Push(wave);

        tokens.Add(c.ComputedTint.AddDisturbance(new MappedObservable<bool, FColor>(c.Exhausted, 
            exh => exh ? new FColor(0.65f, 0.65f, 0.65f, 1f) : FColor.White)));
        Listen(c.ComputedLocation, _ => xml.UpdatedLocations());
        //If loading into a new map, the method is Waiting, but we want to still show the icons.
        bool isFirstState = true;
        Listen(ServiceLocator.Find<ADVManager>().ADVState, s => 
            // ReSharper disable once AccessToModifiedClosure
            SetVisible(s == ADVManager.State.Investigation || (s == ADVManager.State.Waiting && isFirstState)));
        isFirstState = false;
    }

    public void OnBuilt(EmptyNode n) {
        //TODO: generalize thtis icon display
        w = new VisualElement().ConfigureAbsoluteEmpty(false).ConfigureLeftTopListeners(
            n.CreateCenterOffsetChildX(new ConstantObservable<float>(0)),
            n.CreateCenterOffsetChildY(offsetter));
        n.HTML.Add(w);
        w.style.width = 160;
        w.style.height = 160;
        w.style.backgroundImage = new(ADVManager.ADVReferences.talkToIcon);
        w.style.unityBackgroundImageTintColor = entity.ComputedTint.Value._();

        //TransitionHelpers.Apply(t => 40 * M.SinDeg(70 * t), float.PositiveInfinity, x => w.style.top = x)
        //    .Run(this, CoroutineOptions.Droppable);

        /*
    w.transform.ScaleTo(1.02f, 0.1f, Easers.EOutSine, cT)
        .Then(() => NodeHTML.transform.ScaleTo(1f, 0.13f, cT: cT))
        .Run(Controller, new CoroutineOptions(true))*/
    }

    public UIResult OnConfirm() {
        if (ServiceLocator.Find<ADVManager>().ADVState.Value == ADVManager.State.Investigation) {
            hoverActiveAction?.Dispose();
            _ = entity.Manager.ExecuteVN(entity.OnClick).ContinueWithSync(null);
            return new UIResult.StayOnNode();
        } else
            return new UIResult.StayOnNode(UIResult.StayOnNodeType.Silent);
    }
    
    protected override void DoUpdate(float dT) {
        offsetter.Update(dT);
        base.DoUpdate(dT);
    }
    
    public void OnEnter(UINode n) {
        hoverActiveAction?.Dispose();
        hoverActiveAction = entity.Hover?.Enter(entity);
        offsetter.Push(bounce);
    }

    public void OnLeave(UINode n) {
        hoverActiveAction?.Dispose();
        offsetter.Push(wave);
    }

    public void OnPointerDown(UINode n, PointerDownEvent ev) {
        //display.color = new(1, 1, 1, 1f);
    }

    public void OnPointerUp(UINode n, PointerUpEvent ev) {
        //display.color = new(1, 1, 1, 0.8f);
    }
    

    protected override void SetSortingLayer(int layer) { }

    protected override void SetSortingID(int id) { }

    protected override void SetVisible(bool visible) {
        xml.XML.IsVisible.OnNext(visible);
    }

    protected override void SetTint(Color c) {
        if (w != null)
            w.style.unityBackgroundImageTintColor = c;
    }
}

public record InteractableAssertion(ADVManager Manager, BoundedContext<Unit> OnClick) : 
    EntityAssertion<Interactable>(Manager.VNState, OnClick.ID), IAssertion<InteractableAssertion> {
    public bool Exhausted { get; init; } = OnClick.IsCompletedInContexts();
    public Interactable.HoverAction? Hover { get; init; }

    protected override void Bind(Interactable ie) {
        base.Bind(ie);
        ie.Exhausted.OnNext(Exhausted);
        ie.Manager = Manager;
        ie.OnClick = OnClick;
        ie.Hover = Hover;
    }
    
    Task IAssertion.Inherit(IAssertion prev) => AssertionHelpers.Inherit<InteractableAssertion>(prev, this);

    public Task _Inherit(InteractableAssertion prev) => base._Inherit(prev);
}

}