using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Mathematics;
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
    public enum Action {
        APPEAR,
        DISAPPEAR
    }

    public readonly struct AppearRequest {
        public readonly Action action;
        public readonly System.Action? cb;
        public readonly float callbackAtRatio;
        
        public AppearRequest(Action action, float cbRatio, System.Action? cb) {
            this.action = action;
            this.cb = cb;
            this.callbackAtRatio = cbRatio;
        }
    }
    public SpriteRenderer sr = null!;
    public FragmentConfig config = null!;

    public Vector2 moveDirectionMinMax = new(30, 50);
    public float moveDist;
    public float moveTime;
    public float spreadTime;
    private float TotalTime => moveTime + spreadTime;

    public virtual Bounds Bounds => sr.sprite.bounds.MulBy(transform.lossyScale);
    public virtual Vector2 Center => transform.position;
    public virtual (Texture, bool isTemp) Texture() => (sr.sprite.texture, false);
    
    private readonly List<AppearRequest> continuations = new();
    private readonly DMCompactingArray<FragmentRenderInstance> updaters = new();
    private bool isRunning = false;

    protected virtual void Awake() {
        Hide();
    }

    public void Queue(AppearRequest act) {
        continuations.Add(act);
        //Squash
        if (continuations.Count >= 2 &&
            ((continuations[0].action == Action.APPEAR && continuations[1].action == Action.DISAPPEAR) ||
            (continuations[0].action == Action.DISAPPEAR && continuations[1].action == Action.APPEAR))) {
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
        for (int ii = 0; ii < updaters.Count; ++ii)
            if (updaters.GetIfExistsAt(ii, out var u) && u.DoUpdate())
                updaters.Delete(ii);
        
        updaters.Compact();
        base.RegularUpdate();
    }
    
    
    public virtual void Hide() {
        sr.enabled = false;
    }

    public virtual void Show() {
        sr.enabled = true;
    }
    private IEnumerable<Fragment> GenerateFragments(bool invert) {
        var bounds = Bounds;
        float s = config.fragmentRadius * (float)Math.Sqrt(2);
        Vector2 trloc = Center;
        var ixd = Mathf.FloorToInt((s + bounds.size.x) / s);
        var iyd = Mathf.FloorToInt((s + bounds.size.y) / s);
        float Effective01Time(in ParametricInfo bpi) {
            var _ix = bpi.index / iyd;
            var _iy = bpi.index % iyd;
            var xf = _ix / (float) ixd;
            var yf = _iy / (float) iyd;
            var spreadFactor = invert ? (1.9f - xf - yf) : (-0.1f + xf + yf);
            var t = (bpi.t - 0.5f * spreadFactor * spreadTime) / moveTime + 
                    RNG.GetSeededFloat(-0.05f, 0f, RNG.Rehash(bpi.id));
            t = Mathf.Clamp01(t);
            return Easers.EInSine(invert ? 1 - t : t);
        }
        TP mover = bpi => M.CosSinDeg(RNG.GetSeededFloat(moveDirectionMinMax.x, 
            moveDirectionMinMax.y, bpi.id)) * (moveDist * Effective01Time(in bpi));
        BPY scaler = bpi => 1 - Effective01Time(in bpi);
        int ix = 0;
        for (float x = bounds.min.x; x < bounds.max.x + s; x += s, ++ix) {
            int iy = 0;
            for (float y = bounds.min.y; y < bounds.max.y + s; y += s, ++iy) {
                var index = ix * iyd + iy;
                var loc = trloc + new Vector2(x, y);
                var uv = new Vector2(BMath.Ratio(bounds.min.x, bounds.max.x, x), 
                    BMath.Ratio(bounds.min.y, bounds.max.y, y));
                yield return new Fragment(loc, uv, 
                    Mathf.PI/config.fragmentSides, mover, index, null, scaler);
            }
        }
    }

    private void Run(AppearRequest req) {
        var act = req.action;
        isRunning = true;
        if      (act == Action.APPEAR)
            Appear();
        else if (act == Action.DISAPPEAR)
            Disappear();
        else throw new Exception($"Couldn't resolve action {act}");
        if (req.cb != null) {
            if (req.callbackAtRatio > 0)
                RunDroppableRIEnumerator(
                    RUWaitingUtils.WaitFor(req.callbackAtRatio * TotalTime, Cancellable.Null, req.cb));
            else
                req.cb();
        }
    }
    
    [ContextMenu("Disappear")]
    public void Disappear() {
        Show();
        var (tex, temp) = Texture();
        Hide();
        var fri = new FragmentRenderInstance(config, GenerateFragments(false), gameObject.layer, tex, () => {
                if (temp)
                    tex.DestroyTexOrRT();
                Dequeue();
            }, Bounds.size, TotalTime);
        updaters.Add(fri);
    }

    [ContextMenu("Appear")]
    public void Appear() {
        Show();
        var (tex, temp) = Texture();
        Hide();
        var fri = new FragmentRenderInstance(config, GenerateFragments(true), gameObject.layer, tex, () => {
                if (temp)
                    tex.DestroyTexOrRT();
                Show();
                Dequeue();
            }, Bounds.size, TotalTime);
        updaters.Add(fri);
        
    }
    
    
    private void Render(Camera c) {
        if (!Application.isPlaying) return;
        for (int ii = 0; ii < updaters.Count; ++ii) {
            if (updaters.GetIfExistsAt(ii, out var u))
                FragmentRendering.Render(c, u);
        }
    }
    
    public void Clear() {
        for (int ii = 0; ii < updaters.Count; ++ii) {
            if (updaters.GetIfExistsAt(ii, out var u)) {
                u.Destroy();
                updaters.Delete(ii);
            }
        }
        updaters.Compact();
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