using System.Collections;
using DMK.Behavior;
using DMK.Core;
using DMK.Danmaku;
using DMK.Scriptables;
using DMK.Services;
using DMK.SM;
using UnityEngine;

namespace DMK.Player {
public class PlayerHP : CoroutineRegularUpdater {
    public static bool RespawnOnHit => GameManagement.Difficulty.respawnOnDeath;
    public EffectStrategy OnPreHitEffect = null!;
    public EffectStrategy OnHitEffect = null!;
    public EffectStrategy GoldenAuraEffect = null!;
    public float hitInvuln = 0.7f;
    private int hitInvulnFrames;
    private Transform tr = null!;
    private int invulnerabilityCounter = 0;
    private PlayerInput input = null!;

    private bool waitingDeathbomb = false;
    public override int UpdatePriority => UpdatePriorities.PLAYER;
    
    public static readonly Events.IEvent<(int frames, bool effect)> RequestPlayerInvulnerable =
        new Events.Event<(int, bool)>();

    //Calling from PlayerInput instead of Awake prevents binding order errors when
    // the player is replaced at the start of the stage
    public void Setup(PlayerInput inp) {
        tr = transform;
        hitInvulnFrames = Mathf.CeilToInt(hitInvuln * ETime.ENGINEFPS_F);
        input = inp;
        input.hitbox.Player = this;
    }

    protected override void BindListeners() {
        base.BindListeners();
        Listen(RequestPlayerInvulnerable, GoldenAuraInvuln);
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
        if (req.showEffect)
            input.InvokeParentedTimedEffect(GoldenAuraEffect,
                req.frames * ETime.FRAME_TIME).transform.SetParent(tr);
        Invuln(req.frames);
    }

    [ContextMenu("Take 1 Damage")]
    public void _takedmg() => Hit(1);

    public void Hit(int dmg, bool force = false) {
        if (dmg <= 0) return;
        //Log.Unity($"The player has taken a hit for {dmg} hp. Force: {force} Invuln: {invulnerabilityCounter} Deathbomb: {waitingDeathbomb}");
        if (force) _DoHit(dmg);
        else {
            if (invulnerabilityCounter > 0 || waitingDeathbomb) return;
            waitingDeathbomb = true;
            RunRIEnumerator(WaitDeathbomb(dmg));
        }
    }

    public void Graze(int graze) {
        if (graze <= 0 || invulnerabilityCounter > 0) return;
        GameManagement.Instance.AddGraze(graze);
    }

    private void _DoHit(int dmg) {
        BulletManager.AutodeleteCircleOverTime(new SoftcullProperties(input.BPI.loc, 1.1f, 0f, 7f, "cwheel"));
        GameManagement.Instance.AddLives(-dmg);
        DependencyInjection.MaybeFind<IRaiko>()?.ShakeExtra(1.5f, 0.9f);
        Invuln(hitInvulnFrames);
        if (RespawnOnHit) input.RequestNextState(PlayerInput.PlayerState.RESPAWN);
        else input.InvokeParentedTimedEffect(OnHitEffect, hitInvuln);
    }

    private IEnumerator WaitDeathbomb(int dmg) {
        bool didDeathbomb = false;
        int frames = input.OpenDeathbombWindow(() => didDeathbomb = true);
        if (frames > 0) {
            Log.Unity($"The player has {frames} frames to deathbomb", level: Log.Level.DEBUG2);
            OnPreHitEffect.Proc(tr.position, tr.position, 1f);
        }
        while (frames-- > 0) yield return null;
        input.CloseDeathbombWindow();
        if (!didDeathbomb) _DoHit(dmg);
        else Log.Unity($"The player successfully deathbombed", level: Log.Level.DEBUG2);
        waitingDeathbomb = false;
    }
}
}
