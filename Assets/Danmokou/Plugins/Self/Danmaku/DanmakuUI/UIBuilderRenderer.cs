using System;
using System.Collections;
using System.Collections.Generic;
using DMath;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UIElements;

public class UIBuilderRenderer : CoroutineRegularUpdater {
    private static UIBuilderRenderer main;
    
    public PanelSettings settings;
    public Material uiMaterial;

    public RenderTexture unsetRT;
    
    private RenderTexture rt;
    private Cancellable allCancel;
    private Transform tr;
    private SpriteRenderer sr;

    private const int frontOrder = 32000;
    private int normalOrder;

    public enum RAction {
        SLIDE,
        FADE
    }
    
    private readonly Dictionary<RAction, (ICancellee joint, Cancellable root)> cancellers = 
        new Dictionary<RAction, (ICancellee, Cancellable)>();

    private void RemoveCanceller(RAction a, ICancellee c) {
        if (cancellers.TryGetValue(a, out var existing) && existing.joint == c) {
            cancellers.Remove(a);
        }
    }

    private ICancellee CreateNewCanceller(RAction a) {
        if (cancellers.TryGetValue(a, out var existing)) existing.root.Cancel();
        var c = new Cancellable();
        var joint = new JointCancellee(allCancel, c);
        cancellers[a] = (joint, c);
        return joint;
    }

    private Action<bool> WrapDone(RAction action, [CanBeNull] Action<bool> a, out ICancellee c) {
        var cancellee = c = CreateNewCanceller(action);
        return b => {
            RemoveCanceller(action, cancellee);
            a?.Invoke(b);
        };
    }
    
    public static void Slide(Vector2? start, Vector2 end, float time, FXY smooth, [CanBeNull] Action<bool> done) {
        if (start.Try(out var loc)) main.tr.localPosition = loc;
        main.RunRIEnumerator(main._Slide(start, end, time, smooth, 
            main.WrapDone(RAction.SLIDE, done, out var cT), cT));
    }
    private IEnumerator _Slide(Vector2? start, Vector2 end, float time, FXY smooth, Action<bool> done, ICancellee cT)
        => Ease(x => tr.localPosition = x, start ?? tr.localPosition, end, time, smooth, done, cT);
    

    public static void Fade(float? start, float end, float time, FXY smooth, [CanBeNull] Action<bool> done) {
        if (start.Try(out var loc)) main.sr.SetAlpha(loc);
        main.RunRIEnumerator(main._Fade(start, end, time, smooth, 
            main.WrapDone(RAction.FADE, done, out var cT), cT));
    }
    private IEnumerator _Fade(float? start, float end, float time, FXY smooth, Action<bool> done, ICancellee cT)
        => Ease(sr.SetAlpha, start ?? sr.color.a, end, time, smooth, done, cT);

    private IEnumerator Ease(Action<Vector3> apply, Vector3 start, Vector3 end, float time, 
        FXY smooth, Action<bool> done, ICancellee cT) {
        if (cT.Cancelled) { done(false); yield break; }
        for (float t = 0; t < time; t += ETime.FRAME_TIME) {
            apply(Vector2.Lerp(start, end, smooth(t / time)));
            yield return null;
            if (cT.Cancelled) { done(false); yield break; }
        }
        apply(end);
        done(true);
    }
    private IEnumerator Ease(Action<float> apply, float start, float end, float time, 
        FXY smooth, Action<bool> done, ICancellee cT) {
        if (cT.Cancelled) { done(false); yield break; }
        for (float t = 0; t < time; t += ETime.FRAME_TIME) {
            apply(Mathf.Lerp(start, end, smooth(t / time)));
            yield return null;
            if (cT.Cancelled) { done(false); yield break; }
        }
        apply(end);
        done(true);
    }
    
    private void Awake() {
        main = this;
        tr = transform;
        sr = GetComponent<SpriteRenderer>();
        normalOrder = sr.sortingOrder;
        allCancel = new Cancellable();
        RemakeTexture();
    }

    public static void MoveToNormal() {
        main.sr.sortingOrder = main.normalOrder;
    }

    public static void MoveToFront() {
        main.sr.sortingOrder = frontOrder;
    }

    public static void Reconstruct() {
        if (main != null) main.RemakeTexture();
    }

    private void RemakeTexture() {
        if (rt != null) rt.Release();
        rt = MainCamera.DefaultTempRT();
        settings.targetTexture = rt;
        uiMaterial.SetTexture(PropConsts.renderTex, rt);
    }

    protected override void OnDisable() {
        settings.targetTexture = unsetRT;
        uiMaterial.SetTexture(PropConsts.renderTex, unsetRT);
        allCancel?.Cancel();
        base.OnDisable();
    }

    public override bool UpdateDuringPause => true;
    public override int UpdatePriority => UpdatePriorities.SLOW;
}