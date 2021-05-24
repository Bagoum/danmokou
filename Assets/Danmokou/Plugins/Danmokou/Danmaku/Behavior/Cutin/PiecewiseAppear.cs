using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Graphics;
using Danmokou.SM;
using UnityEngine;
using static Danmokou.Graphics.FragmentRendering;

namespace Danmokou.Behavior.Display {
public class PiecewiseAppear : CoroutineRegularUpdater {
    public enum AppearAction {
        APPEAR,
        DISAPPEAR
    }

    public readonly struct AppearRequest {
        public readonly AppearAction action;
        public readonly Action? cb;
        public readonly float callbackAtRatio;
        
        public AppearRequest(AppearAction action, float cbRatio, Action? cb) {
            this.action = action;
            this.cb = cb;
            this.callbackAtRatio = cbRatio;
        }
    }
    public SpriteRenderer sr = null!;
    public FragmentConfig config = null!;

    public Vector2 moveDirectionMinMax = new Vector2(30, 50);
    public float moveDist;
    public float moveTime;
    public float spreadTime;
    private float TotalTime => moveTime + spreadTime;
    
    private const string uiLayer = "UI";
    private readonly List<AppearRequest> continuations = new List<AppearRequest>();

    private readonly DMCompactingArray<FragmentRenderInstance> updaters = new DMCompactingArray<FragmentRenderInstance>();

    private bool isRunning = false;

    protected virtual void Awake() {
        Hide();
    }

    public void Queue(AppearRequest act) {
        continuations.Add(act);
        //Squash
        if (continuations.Count >= 2 &&
            ((continuations[0].action == AppearAction.APPEAR && continuations[1].action == AppearAction.DISAPPEAR) ||
            (continuations[0].action == AppearAction.DISAPPEAR && continuations[1].action == AppearAction.APPEAR))) {
            var cb1 = continuations[0].cb;
            var cb2 = continuations[1].cb;
            continuations.RemoveAt(0);
            continuations.RemoveAt(0);
            cb1?.Invoke();
            cb2?.Invoke();
        }
    }


    private void Dequeue() {
        if (continuations.Count > 0) {
            var nxt = continuations[0];
            continuations.RemoveAt(0);
            Run(nxt);
        } else
            isRunning = false;
    }

    public override void RegularUpdate() {
        //The reason this is done in RU instead of immediately on Queue
        // is to allow successive Appear/Disappears in one frame to cancel out
        // in the ReturnTo menu situation.
        if (!isRunning && continuations.Count > 0)
            Dequeue();
        for (int ii = 0; ii < updaters.Count; ++ii) {
            if (!updaters.Data[ii].MarkedForDeletion) {
                if (updaters[ii].DoUpdate())
                    updaters.Data[ii].MarkForDeletion();
            }
        }
        updaters.Compact();
        base.RegularUpdate();
    }
    
    
    public virtual void Hide() {
        sr.enabled = false;
    }

    public virtual void Show() {
        sr.enabled = true;
    }

    public virtual Bounds Bounds => sr.sprite.bounds.MulBy(transform.lossyScale);
    public virtual Vector2 Center => transform.position;
    public virtual (Texture, bool isTemp) Texture => (sr.sprite.texture, false);

    private IEnumerable<Fragment> GenerateFragments(bool invert) {
        var bounds = Bounds;
        float s = config.fragmentRadius * (float)Math.Sqrt(2);
        Vector2 trloc = Center;
        var ixd = Mathf.FloorToInt((s + bounds.max.x - bounds.min.x) / s);
        var iyd = Mathf.FloorToInt((s + bounds.max.y - bounds.min.y) / s);
        (float xf, float yf) GetRatios(in ParametricInfo bpi) {
            var _ix = bpi.index / iyd;
            var _iy = bpi.index % iyd;
            return (_ix / (float) ixd, _iy / (float) iyd);
        }
        float Effective01Time(in ParametricInfo bpi) {
            var (xf, yf) = GetRatios(in bpi);
            var t = (bpi.t - 0.5f * (1.9f - xf - yf) * spreadTime) / moveTime + 
                   RNG.GetSeededFloat(-0.05f, 0f, RNG.Rehash(bpi.id));
            t = Mathf.Clamp01(t);
            return M.EInSine(invert ? 1 - t : t);
        }
        TP mover = bpi => {
            var endpoint = moveDist * M.CosSinDeg(RNG.GetSeededFloat(moveDirectionMinMax.x, 
                moveDirectionMinMax.y, bpi.id));
            return Vector2.Lerp(Vector2.zero, endpoint, Effective01Time(in bpi));
        };
        BPY scaler = t => Mathf.Lerp(1f, 0f, Effective01Time(in t));
        int ix = 0;
        for (float x = bounds.min.x; x < bounds.max.x + s; x += s, ++ix) {
            int iy = 0;
            for (float y = bounds.min.y; y < bounds.max.y + s; y += s, ++iy) {
                var index = ix * iyd + iy;
                var loc = trloc + new Vector2(x, y);
                var uv = new Vector2(M.Ratio(bounds.min.x, bounds.max.x, x), 
                    M.Ratio(bounds.min.y, bounds.max.y, y));
                yield return new Fragment(loc, uv, 
                    Mathf.PI/4, mover, index, null, scaler);
            }
        }
    }

    private void Run(AppearRequest req) {
        var act = req.action;
        isRunning = true;
        if      (act == AppearAction.APPEAR)
            Appear();
        else if (act == AppearAction.DISAPPEAR)
            Disappear();
        else throw new Exception($"Couldn't resolve action {act}");
        if (req.cb != null) {
            if (req.callbackAtRatio > 0)
                RunDroppableRIEnumerator(
                    WaitingUtils.WaitFor(req.callbackAtRatio * TotalTime, Cancellable.Null, req.cb));
            else
                req.cb();
        }
    }
    
    [ContextMenu("Disappear")]
    public void Disappear() {
        Show();
        var (tex, temp) = Texture;
        var fri = new FragmentRenderInstance(config, GenerateFragments(false), uiLayer, tex, () => {
                if (temp)
                    tex.DestroyTexOrRT();
                Dequeue();
            },
            Bounds.extents * 2, TotalTime);
        Hide();
        updaters.Add(fri);
    }

    [ContextMenu("Appear")]
    public void Appear() {
        Show();
        var (tex, temp) = Texture;
        var fri = new FragmentRenderInstance(config, GenerateFragments(true), uiLayer, tex, () => {
                if (temp)
                    tex.DestroyTexOrRT();
                Show();
                Dequeue();
            },
            Bounds.extents * 2, TotalTime);
        Hide();
        updaters.Add(fri);
        
    }
    
    public void Clear() {
        for (int ii = 0; ii < updaters.Count; ++ii) {
            if (!updaters.Data[ii].MarkedForDeletion) {
                updaters[ii].Destroy();
                updaters.Data[ii].MarkForDeletion();
            }
        }
        updaters.Compact();
    }
    
    
    private void Render(Camera c) {
        if (!Application.isPlaying) return;
        //Effects render to LowEffects
        for (int ii = 0; ii < updaters.Count; ++ii) {
            if (!updaters.Data[ii].MarkedForDeletion)
                FragmentRendering.Render(c, updaters[ii]);
        }
    }

    protected override void OnEnable() {
        Camera.onPreCull += Render;
        base.OnEnable();
    }

    protected override void OnDisable() {
        Clear();
        Camera.onPreCull -= Render;
        base.OnDisable();
    }
    
}

}