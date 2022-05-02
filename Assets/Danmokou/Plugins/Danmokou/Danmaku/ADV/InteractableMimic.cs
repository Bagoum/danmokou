using System;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
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

public abstract record InteractableType {
    public abstract Sprite Icon { get; }
    public virtual bool UseWaveEffect => true;

    public record Dialogue : InteractableType {
        public override Sprite Icon => ADVManager.ADVReferences.talkToIcon;
    }

    public record Map(bool Current) : InteractableType {
        public override Sprite Icon => 
            Current ? ADVManager.ADVReferences.mapCurrentIcon : ADVManager.ADVReferences.mapNotCurrentIcon;
        public override bool UseWaveEffect => false;
    }
}

/// <summary>
/// A VN entity that can be clicked on to trigger something (generally a <see cref="BoundedContext{T}"/>).
/// </summary>
public class Interactable : Rendered {
    public ADVManager Manager { get; set; } = null!;
    public Action OnClick { get; set; } = null!;
    public Evented<bool> Exhausted { get; set; } = new(false);
    public InteractableType Type { get; set; } = null!;
    
    public HoverAction? Hover { get; set; }
    
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
}


public class InteractableMimic : RenderedMimic, IFixedXMLReceiver {
    public override Type[] CoreTypes => new[] {typeof(Interactable)};
    public override string SortingLayerFromPrefab => "";

    public FixedXMLHelper xml = null!;
    
    private Interactable entity = null!;
    
    private readonly PushLerperF<float> offsetter = new(0.3f, Mathf.LerpUnclamped);
    private readonly PushLerper<float> borderColor = new(0.3f, Mathf.LerpUnclamped);
    private static readonly Easer b = Easers.CEOutBounce(0, 0.45f, 0.7f, 0.85f, 1f);
    private readonly Func<float, float> bounce = t => 160 * (-1f + b(Mathf.Clamp01(M.Mod(2f, t) / 1.2f)));
    private readonly Func<float, float> _wave = t => 20 * M.SinDeg(50 * t);
    private readonly Func<float, float> _wave0 = t => 0;
    private Func<float, float> Wave => (entity.Type ?? throw new Exception("hello")).UseWaveEffect ? _wave : _wave0;
    private VisualElement? w = null!;

    private IDisposable? hoverActiveAction;

    public override void _Initialize(IEntity ent) => Initialize((ent as Interactable)!);

    
    private void Initialize(Interactable c) {
        base.Initialize(entity = c);
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
            s == ADVManager.State.Investigation || 
            (s == ADVManager.State.Waiting && isFirstState && man.VNState.Contexts.Count == 0 && 
             !man.ExecAdv!.MapStates.CurrentMapState.HasEntryVN))));
        isFirstState = false;
    }

    public void OnBuilt(EmptyNode n) {
        var wb = new VisualElement().ConfigureAbsoluteEmpty(false).ConfigureLeftTopListeners(
            n.CreateCenterOffsetChildX(new ConstantObservable<float>(0)),
            n.CreateCenterOffsetChildY(this.offsetter));
        n.HTML.Add(wb);
        borderColor.Subscribe(a => wb.SetBorder(new Color(0.6f, 0f, 0.5f, a), 12));
        w = new VisualElement().ConfigureEmpty(false);
        wb.Add(w);
        w.style.width = 160;
        w.style.height = 160;
        w.style.backgroundImage = new(entity.Type.Icon);
        w.style.unityBackgroundImageTintColor = entity.ComputedTint.Value._();

        //TransitionHelpers.Apply(t => 40 * M.SinDeg(70 * t), float.PositiveInfinity, x => w.style.top = x)
        //    .Run(this, CoroutineOptions.Droppable);

        /*
    w.transform.ScaleTo(1.02f, 0.1f, Easers.EOutSine, cT)
        .Then(() => NodeHTML.transform.ScaleTo(1f, 0.13f, cT: cT))
        .Run(Controller, new CoroutineOptions(true))*/
    }

    public UIResult OnConfirm(UINode n) {
        if (ServiceLocator.Find<ADVManager>().ADVState.Value == ADVManager.State.Investigation) {
            OnLeave(n);
            entity.OnClick();
            return new UIResult.StayOnNode();
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
        borderColor.Push(0.7f);
    }

    public void OnLeave(UINode n) {
        hoverActiveAction?.Dispose();
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

public record InteractableAssertion(ADVManager Manager, Action OnClick, string ID) : 
    EntityAssertion<Interactable>(Manager.VNState, ID), IAssertion<InteractableAssertion> {
    public InteractableType Type { get; init; } = new InteractableType.Dialogue();
    public bool Exhausted { get; init; }
    public Interactable.HoverAction? Hover { get; init; }

    protected override void Bind(Interactable ie) {
        base.Bind(ie);
        ie.Exhausted.OnNext(Exhausted);
        ie.Manager = Manager;
        ie.OnClick = OnClick;
        ie.Hover = Hover;
        ie.Type = Type;
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


}