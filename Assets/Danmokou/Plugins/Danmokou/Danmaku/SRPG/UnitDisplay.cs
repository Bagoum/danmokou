using System;
using System.Collections;
using BagoumLib.Events;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Graphics;
using Danmokou.SRPG;
using UnityEngine;
using Object = UnityEngine.Object;

public interface IUnitDisplay {
    void RunRIEnumerator(IEnumerator ienum);
    void SetLocation(Vector2 worldLoc);
    void Destroy();
}

public class UnitDisplay : CoroutineRegularUpdater, IUnitDisplay {
    public Transform Tr { get; private set; } = null!;
    private SpriteRenderer sr = null!;
    private Material mat = null!;
    private MaterialPropertyBlock pb = null!;
    private float t;
    private Unit unit = null!;
    private readonly Evented<UnitStatus> status = new(UnitStatus.NotMyTurn);
    private readonly PushLerper<Color> color = new(0.1f);

    private void Awake() {
        Tr = transform;
        sr = GetComponent<SpriteRenderer>();
        mat = sr.material; //non-shared for keywords
        sr.GetPropertyBlock(pb = new());
    }

    public void Initialize(Unit u) {
        unit = u;
        if (u.Team.FlipSprite)
            FlipSprite();
        Listen(color, c => sr.color = c);
        Listen(status, s => {
            mat.SetOrUnsetKeyword(s is UnitStatus.NotMyTurn, "FT_GRAYSCALE");
            color.Push(unit.Status switch {
                UnitStatus.CanMove => Color.white,
                UnitStatus.Exhausted or UnitStatus.NotMyTurn => new Color(0.7f, 0.7f, 0.7f),
                _ => throw new ArgumentOutOfRangeException()
            });
        });
    }

    public void SetLocation(Vector2 worldLoc) => Tr.position = worldLoc;

    public override void RegularUpdate() {
        base.RegularUpdate();
        status.PublishIfNotSame(unit.Status);
        color.Update(ETime.FRAME_TIME);
        pb.SetFloat(PropConsts.time, t += ETime.FRAME_TIME);
        sr.SetPropertyBlock(pb);
    }

    public void FlipSprite() {
        var sc = Tr.localScale;
        sc.x *= -1;
        Tr.localScale = sc;
    }

    public void Destroy() => Object.Destroy(this);
}