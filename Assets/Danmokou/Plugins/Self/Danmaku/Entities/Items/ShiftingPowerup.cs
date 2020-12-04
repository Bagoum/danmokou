using System;
using System.Collections;
using System.Collections.Generic;
using DMath;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmaku {
public class ShiftingPowerup : BouncyItem {
    [Serializable]
    public struct Variant {
        public Sprite sprite;
        public Enums.Subshot type;
        public string color;
    }

    public Variant[] variants;
    private int currVariant;
    private PowerUp effect;
    public GameObject switchTell;
    public float switchTellScale;
    public float switchTellIterations;
    public SFXConfig onSwitch;
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
    
    public override void Initialize(Vector2 root, Vector2 targetOffset, [CanBeNull] PoC collectionPoint = null) {
        base.Initialize(root, targetOffset, collectionPoint);
        currVariant = RNG.GetInt(0, variants.Length);
        timeUntilSwitch = timePerVariant;
        UpdateVariant();
    }

    protected override void CollectMe() {
        if (effect != null) effect.InvokeCull();
        GameManagement.campaign.SetSubshot(variants[currVariant].type);
        base.CollectMe();
    }

    private void UpdateVariant() {
        var v = variants[currVariant];
        sr.sprite = v.sprite;
        if (effect != null) {
            effect.InvokeCull();
        }
        if (time + timePerVariant < StopShiftingAfter) {
            effect = Instantiate(switchTell, tr).GetComponent<PowerUp>();
            effect.transform.localScale = new Vector3(switchTellScale, switchTellScale, switchTellScale);
            effect.Initialize(ReflWrap<TP4>.Wrap(v.color), timePerVariant, switchTellIterations);
        }
        SFXService.Request(onSwitch);
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