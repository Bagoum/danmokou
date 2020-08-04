using System;
using System.Collections;
using UnityEngine;

public class PlayerHP : CoroutineRegularUpdater {
    public EffectStrategy OnHitEffect;
    public EffectStrategy GoldenAuraEffect;
    public float hitInvuln = 0.7f;
    private int hitInvulnFrames;
    private Transform tr;
    private int invulnerabilityCounter = 0;

    private DeletionMarker<Action<int>> damageListener;
    private DeletionMarker<Action<int, bool>> invulnListener;


    public override int UpdatePriority => UpdatePriorities.PLAYER;
    private void Awake() {
        tr = transform;
        hitInvulnFrames = Mathf.CeilToInt(hitInvuln * ETime.ENGINEFPS);
    }

    protected override void OnEnable() {
        damageListener = Core.Events.TryHitPlayer.Listen(Hit);
        invulnListener = Core.Events.MakePlayerInvincible.Listen(GoldenAuraInvuln);
        base.OnEnable();
    }

    private IEnumerator WaitOutInvuln(int frames) {
        for (int ii = frames; ii > 0; --ii) yield return null;
        --invulnerabilityCounter;
    }

    private void Invuln(int frames) {
        ++invulnerabilityCounter;
        RunDroppableRIEnumerator(WaitOutInvuln(frames));
    }

    private void GoldenAuraInvuln(int frames, bool showEffect) {
        //TODO need a better way of parenting effects
        if (showEffect) InvokeEffectWithTime(GoldenAuraEffect, frames * ETime.FRAME_TIME).transform.SetParent(tr);
        Invuln(frames);
    }

    private GameObject InvokeEffectWithTime(EffectStrategy effect, float time) {
        var v = tr.position;
        var effectGO = effect.ProcNotNull(v, v, 0);
        effectGO.transform.SetParent(tr);
        var animator = effectGO.GetComponent<TimeBoundAnimator>();
        if (animator != null) animator.AssignTime(time);
        return effectGO;
    }

    public void Hit(int dmg) {
        if (dmg == 0) return;
        if (dmg > 0 && invulnerabilityCounter > 0) return;
        GameManagement.campaign.AddLives(-dmg);
        if (dmg > 0) {
            InvokeEffectWithTime(OnHitEffect, hitInvuln);
            Invuln(hitInvulnFrames);
        }
    }

    protected override void OnDisable() {
        damageListener.MarkForDeletion();
        invulnListener.MarkForDeletion();
        base.OnDisable();
    }
}
