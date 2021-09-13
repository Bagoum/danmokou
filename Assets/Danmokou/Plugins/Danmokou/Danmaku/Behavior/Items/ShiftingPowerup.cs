using System;
using System.Linq;
using BagoumLib.Cancellation;
using Danmokou.Behavior.Display;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Options;
using Danmokou.DMath;
using Danmokou.Player;
using Danmokou.Reflection;
using Danmokou.Scriptables;
using Danmokou.Services;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Behavior.Items {
public class ShiftingPowerup : BouncyItem {
    protected override ItemType Type => ItemType.POWERUP_SHIFT;
    [Serializable]
    public struct Variant {
        public Sprite sprite;
        public Subshot type;
        public string color;
    }

    [ReflectInto(typeof(TP4))] [UsedImplicitly] public string[] _variantColors => 
        variants.Select(v => v.color).ToArray();

    public Variant[] variants = null!;
    private int currVariant;
    private PowerAura effect = null!;
    public GameObject switchTell = null!;
    public float switchTellScale;
    public float switchTellIterations;
    public SFXConfig? onSwitch;
    public float timePerVariant;
    private float timeUntilSwitch;
    private float StopShiftingAfter => timePerVariant * (2 * variants.Length - 1);
    protected override float StopBouncingAfter => StopShiftingAfter;

    protected override void Awake() {
        for (int ii = 0; ii < variants.Length; ++ii) {
            ReflWrap<TP4>.Load(variants[ii].color);
        }
        base.Awake();
    }
    
    public override void Initialize(Vector2 root, Vector2 targetOffset, PoC? collectionPoint = null) {
        base.Initialize(root, targetOffset, collectionPoint);
        currVariant = RNG.GetInt(0, variants.Length);
        timeUntilSwitch = timePerVariant;
        UpdateVariant();
    }

    protected override void CollectMe(PlayerController collector) {
        if (effect != null) effect.InvokeCull();
        collector.SetSubshot(variants[currVariant].type);
        base.CollectMe(collector);
    }

    private void UpdateVariant() {
        var v = variants[currVariant];
        sr.sprite = v.sprite;
        if (effect != null) {
            effect.InvokeCull();
        }
        if (time + timePerVariant < StopShiftingAfter) {
            effect = Instantiate(switchTell, tr).GetComponent<PowerAura>();
            var opts = new PowerAuraOptions(new[] {
                PowerAuraOption.Color(ReflWrap<TP4>.Wrap(v.color)),
                PowerAuraOption.Time(_ => timePerVariant),
                PowerAuraOption.Iterations(_ => switchTellIterations),
                PowerAuraOption.Scale(_ => switchTellScale),
            });
            effect.Initialize(new RealizedPowerAuraOptions(opts, GenCtx.Empty, Vector2.zero, Cancellable.Null, null!));
        }
        ServiceLocator.SFXService.Request(onSwitch);
    }

    public override void RegularUpdate() {
        base.RegularUpdate();
        if ((timeUntilSwitch -= ETime.FRAME_TIME) <= 0 && time < StopShiftingAfter) {
            timeUntilSwitch = timePerVariant;
            currVariant = (currVariant + 1) % variants.Length;
            UpdateVariant();
        }
    }
}
}