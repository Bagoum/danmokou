using System;
using System.Collections;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Functional;
using BagoumLib.Mathematics;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Graphics;
using Danmokou.Services;
using UnityEngine;
using static Danmokou.Services.GameManagement;
using static Danmokou.DMath.LocationHelpers;

namespace Danmokou.Player {
public partial class PlayerController {
    public abstract record PlayerState {

        public record NULL : PlayerState;
        public record Normal : PlayerState;

        public record WitchTime(IDisposable MeterToken) : PlayerState;

        public record Respawn : PlayerState;
    }
    
    public class PlayerStateFlow : StateFlow<PlayerState> {
        private readonly PlayerController p;

        public PlayerStateFlow(PlayerController p) : base(new PlayerState.Normal()) {
            this.p = p;
        }

        protected override void RunState(Maybe<PlayerState> prev) {
            if (State is PlayerState.NULL)
                return;
            p.RunRIEnumerator(State switch {
                PlayerState.Normal => p.StateNormal(),
                PlayerState.WitchTime wt => p.StateWitchTime(wt),
                PlayerState.Respawn => p.StateRespawn(),
                _ => throw new Exception($"Unhandled player state: {State}")
            });
        }
    }
    
    

    //Assumption for state enumerators is that the token is not initially cancelled.
    private IEnumerator StateNormal() {
        while (true) {
            if (IsTryingWitchTime) {
                if (GameManagement.Instance.MeterF.TryStartMeter() is { } meterToken) {
                    StateFlow.SetNext(new PlayerState.WitchTime(meterToken));
                } else
                    PlayerMeterFailed.OnNext(default);
            }
            if (StateFlow.GoToNextIfCancelled()) yield break;
            yield return null;
        }
    }
    private IEnumerator StateWitchTime(PlayerState.WitchTime st) {
        GameManagement.Instance.LastMeterStartFrame = ETime.FrameNumber;
        speedLines.Play();
        using var t = ETime.Slowdown.AddConst(WitchTimeSlowdown);
        using var _mt = st.MeterToken;
        var displayCt = new Cancellable();
        RunDroppableRIEnumerator(ShowMeterDisplay(null, displayCt, 0.25f));
        PlayerActivatedMeter.OnNext(default);
        for (int f = 0; !StateFlow.Cancelled && IsTryingWitchTime && Instance.MeterF.TryUseMeterFrame(); ++f) {
            SpawnedShip.MaybeDrawWitchTimeGhost(f);
            MeterIsActive.OnNext(Instance.MeterF.EnoughMeterToUse ? meterDisplay : meterDisplayInner);
            yield return null;
        }
        displayCt.Cancel();
        PlayerDeactivatedMeter.OnNext(default);
        speedLines.Stop();
        StateFlow.GoToNextWithDefault(new PlayerState.Normal());
    }
    
    private IEnumerator StateRespawn() {
        SpawnedShip.RespawnOnHitEffect.Proc(Hurtbox.location, Hurtbox.location, 0f);
        InputInControl = InputInControlMethod.NONE_SINCE_RESPAWN;
        PastDirections.Clear();
        PastPositions.Clear();
        MarisaADirections.Clear();
        MarisaAPositions.Clear();
        PastDirections.Add(Vector2.down);
        MarisaADirections.Add(Vector2.down);
        for (float t = 0; t < RespawnFreezeTime && !StateFlow.Cancelled; t += ETime.FRAME_TIME) 
            yield return null;
        if (StateFlow.GoToNextIfCancelled())
            yield break;
        //Don't update the hitbox location
        tr.position = new Vector2(0f, 100f);
        InvokeParentedTimedEffect(SpawnedShip.RespawnAfterEffect, hitInvuln - RespawnFreezeTime);
        //Respawn doesn't respect state cancellations
        for (float t = 0; t < RespawnDisappearTime && !StateFlow.Cancelled; t += ETime.FRAME_TIME) 
            yield return null;
        for (float t = 0; t < RespawnMoveTime && !StateFlow.Cancelled; t += ETime.FRAME_TIME) {
            var nxtPos = Vector2.Lerp(RespawnStartLoc, RespawnEndLoc, t / RespawnMoveTime);
            tr.position = nxtPos;
            LocationHelpers.UpdateTruePlayerLocation(nxtPos);
            PastPositions.Add(nxtPos);
            MarisaAPositions.Add(nxtPos);
            yield return null;
        }
        if (!StateFlow.GoToNextIfCancelled()) {
            SetLocation(RespawnEndLoc);
            StateFlow.GoToNext(new PlayerState.Normal());
        }
    }
    
    private IEnumerator ShowMeterDisplay(float? maxTime, ICancellee cT, float fadeInOver=0) {
        meterDisplayOpacity.Push(1);
        for (float t = 0; t < (maxTime ?? float.PositiveInfinity) && !cT.Cancelled; t += ETime.FRAME_TIME) {
            float meterDisplayRatio = fadeInOver <= 0 ? 1 : Easers.EOutSine(Mathf.Clamp01(t / fadeInOver));
            meterPB.SetFloat(PropConsts.FillRatio, Instance.MeterF.VisibleMeter.Value * meterDisplayRatio);
            meter.SetPropertyBlock(meterPB);
            yield return null;
        }
        meterDisplayOpacity.Push(0);
    }
    
}
}