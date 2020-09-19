using System;
using System.Collections;
using Danmaku;
using UnityEngine;

public class PlayerHP : CoroutineRegularUpdater {
    public static bool RespawnOnHit { get; } = false;
    public EffectStrategy OnPreHitEffect;
    public EffectStrategy OnHitEffect;
    public EffectStrategy GoldenAuraEffect;
    public float hitInvuln = 0.7f;
    private int hitInvulnFrames;
    private Transform tr;
    private int invulnerabilityCounter = 0;
    private PlayerInput input;

    private DeletionMarker<Action<(int, bool)>> damageListener;
    private DeletionMarker<Action<(int, bool)>> invulnListener;


    public override int UpdatePriority => UpdatePriorities.PLAYER;
    private void Awake() {
        tr = transform;
        hitInvulnFrames = Mathf.CeilToInt(hitInvuln * ETime.ENGINEFPS);
        input = GetComponent<PlayerInput>();
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

    private void GoldenAuraInvuln((int frames, bool showEffect) req) {
        if (req.showEffect) input.InvokeParentedTimedEffect(GoldenAuraEffect, 
            req.frames * ETime.FRAME_TIME).transform.SetParent(tr);
        Invuln(req.frames);
    }

    [ContextMenu("Take 1 Damage")]
    public void _takedmg() => Hit((1, false));

    private bool waitingDeathbomb = false;
    public void Hit((int dmg, bool force) req) {
        if (req.dmg <= 0) return;
        if (req.force) _DoHit(req.dmg);
        else {
            if (invulnerabilityCounter > 0 || waitingDeathbomb) return;
            waitingDeathbomb = true;
            RunRIEnumerator(WaitDeathbomb(req.dmg));
        }
    }

    private void _DoHit(int dmg) {
        GameManagement.campaign.AddLives(-dmg);
        RaikoCamera.ShakeExtra(2, 1f);
        Invuln(hitInvulnFrames);
        if (RespawnOnHit) input.RequestNextState(PlayerInput.PlayerState.RESPAWN);
        else input.InvokeParentedTimedEffect(OnHitEffect, hitInvuln);
    }

    private IEnumerator WaitDeathbomb(int dmg) {
        bool didDeathbomb = false;
        int frames = input.OpenDeathbombWindow(() => didDeathbomb = true);
        Log.Unity($"The player has {frames} frames to deathbomb", level: Log.Level.DEBUG2);
        if (frames > 0) OnPreHitEffect.Proc(tr.position, tr.position, 1f);
        while (frames-- > 0) yield return null;
        input.CloseDeathbombWindow();
        if (!didDeathbomb) _DoHit(dmg);
        else Log.Unity($"The player successfully deathbombed", level: Log.Level.DEBUG2);
        waitingDeathbomb = false;
    }

    protected override void OnDisable() {
        damageListener.MarkForDeletion();
        invulnListener.MarkForDeletion();
        base.OnDisable();
    }
}
