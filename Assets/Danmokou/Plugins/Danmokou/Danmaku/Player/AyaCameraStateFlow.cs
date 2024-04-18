using System;
using System.Collections;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Functional;
using BagoumLib.Mathematics;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Graphics;
using Danmokou.Services;
using Danmokou.UI;
using UnityEngine;

namespace Danmokou.Player {
public partial class AyaCamera {
    public record State {
        public record NULL : State;
        public record Normal : State;
        public record Charge : State;
        public record Firing : State;

        public record TakePicture(float scale) : State;
        public record Reload(bool success) : State;
    }

    public class AyaCameraStateFlow : StateFlow<State> {
        private readonly AyaCamera cam;

        public AyaCameraStateFlow(AyaCamera cam) : base(new State.Normal()) {
            this.cam = cam;
        }
        
        protected override void RunState(Maybe<State> prev) {
            if (State is State.NULL)
                return;
            cam.RunRIEnumerator(State switch {
                State.Normal => cam.UpdateNormal(),
                State.Charge => cam.UpdateCharge(),
                State.Firing => cam.UpdateFire(),
                State.TakePicture ff => cam.DoTakePicture(ff.scale),
                State.Reload rel => cam.UpdateReload(rel.success),
                _ => throw new Exception($"Unhandled camera state: {State}")
            });
        }
    }
    
    private IEnumerator UpdateNormal() {
        viewfinder.gameObject.layer = lowCameraLayer;
        bool alreadyCharging = player.IsFiring;
        while (!StateFlow.GoToNextIfCancelled()) {
            alreadyCharging &= player.IsFiring;
            if (ChargeFull && !player.IsFocus && player.IsFiring && !alreadyCharging) {
                StateFlow.GoToNext(new State.Firing());
                yield break;
            } else if (InputCharging) {
                StateFlow.GoToNext(new State.Charge());
                yield break;
            }
            yield return null;
        }
    }
    
    private IEnumerator UpdateCharge() {
        using var cToken = new Cancellable();
        ISFXService.SFXService.RequestSource(whileCharge, cToken);
        while (!StateFlow.Cancelled && InputCharging) yield return null;
        StateFlow.GoToNextWithDefault(new State.Normal());
    }
    
    private IEnumerator UpdateFire() {
        using var slowdownToken = ETime.Slowdown.AddConst(0.5f);
        viewfinder.gameObject.layer = highCameraLayer;
        using var cToken = new Cancellable();
        ISFXService.SFXService.RequestSource(whileFire, cToken);
        for (float t = 0f; t < cameraLerpDownTime && !StateFlow.Cancelled; t += ETime.FRAME_TIME) {
            float scale = M.Lerp(cameraFireSize, 1f, Easers.EInSine(t / cameraLerpDownTime));
            charge = 100 * (1 - Easers.EInSine(t / cameraLerpDownTime));
            viewfinder.localScale = new Vector3(scale, scale, scale);
            tr.position = location += cameraFireControlSpeed * ETime.FRAME_TIME * player.DesiredMovement01;
            var vf = ViewfinderRect(scale);
            //take shot by letting go of fire key
            if (!player.IsFiring) {
                StateFlow.GoToNext(new State.TakePicture(scale));
                yield break;
            } else {
                var enemies = Enemy.FrozenEnemies;
                for (int ii = 0; ii < enemies.Count; ++ii) {
                    enemies[ii].enemy.ShowCrosshairIfViewfinderHits(vf);
                }
            }
            yield return null;
        }
        var _enemies = Enemy.FrozenEnemies;
        for (int ii = 0; ii < _enemies.Count; ++ii) {
            _enemies[ii].enemy.HideViewfinderCrosshair();
        }
        if (!StateFlow.GoToNextIfCancelled()) {
            ISFXService.SFXService.Request(onTimeout);
            StateFlow.GoToNext(new State.Normal());
        }
    }
    
    
    private IEnumerator DoTakePicture(float scale) {
        var success = TakePicture_Enemies(scale);
        viewfinderSR.enabled = false;
        text.enabled = false;
        var photoRect = ViewfinderRect(scale);
        var photoTex = ServiceLocator.Find<IScreenshotter>().Screenshot(photoRect);
        var photo = new AyaPhoto(photoTex.IntoTex(), photoRect, success && GameManagement.Instance.Request?.replay is null);
        PhotoTaken.OnNext((photo, success));
        photoTex.Release();
        var pphoto = GameObject.Instantiate(pinnedPhotoPrefab).GetComponent<AyaPinnedPhoto>();
        pphoto.Initialize(photo, location, success ? 
            ServiceLocator.FindOrNull<IAyaPhotoBoard>()?.NextPinLoc(pphoto) : 
            null);
        viewfinderSR.enabled = true;
        text.enabled = true;
        var freezer = ServiceLocator.Find<FreezeFrameHelper>();
        freezer.CreateFreezeFrame(freezeTime);
        freezer.RunDroppableRIEnumerator(DoFlash(flashTime, success));
        //Wait until after the freeze to delete bullets
        yield return null;
        TakePicture_Delete(scale);
        StateFlow.GoToNextWithDefault(new State.Reload(success));
    }
    private IEnumerator UpdateReload(bool shotHit) {
        viewfinder.gameObject.layer = lowCameraLayer;
        float t = shotHit ? 1.2f : 0.4f;
        charge = Math.Min(charge, shotHit ? 0 : 50);
        viewfinder.localScale = new Vector3(1f, 1f, 1f);
        for (float elapsed = 0f; elapsed < t && !StateFlow.Cancelled; elapsed += ETime.FRAME_TIME) {
            yield return null;
        }
        StateFlow.GoToNextWithDefault(new State.Normal());
    }
}
}