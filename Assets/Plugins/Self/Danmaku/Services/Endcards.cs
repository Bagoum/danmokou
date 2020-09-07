using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using ExBPY = System.Func<DMath.TExPI, TEx<float>>;
using ExTP = System.Func<DMath.TExPI, TEx<UnityEngine.Vector2>>;
using ExPred = System.Func<DMath.TExPI, TEx<bool>>;
using Danmaku;
using LocationService = Danmaku.LocationService;
using static DMath.ExM;

namespace Danmaku {
[Serializable]
public struct Endcard {
    public string name;
    public Sprite image;
}
public class Endcards : CoroutineRegularUpdater {
    private static Endcards main;
    private SpriteRenderer sr;

    public Endcard[] cards;
    private static readonly Dictionary<string, Sprite> images = new Dictionary<string, Sprite>();
    private void Awake() {
        main = this;
        sr = GetComponent<SpriteRenderer>();
        
        images.Clear();
        foreach (var c in cards) images[c.name] = c.image;
    }

    public static void Activate() {
        main.sr.enabled = true;
    }

    public static void Deactivate() {
        main.sr.enabled = false;
    }

    public static void FadeIn(float t, string key, ICancellee cT, Action cb) {
        main.sr.sprite = images.GetOrThrow(key, "Endcards");
        main.RunRIEnumerator(main._FadeIn(t, cT, cb));
    }
    public static void FadeOut(float t, ICancellee cT, Action cb) {
        main.RunRIEnumerator(main._FadeOut(t, cT, cb));
    }

    private IEnumerator _FadeIn(float t, ICancellee cT, Action cb) {
        for (float elapsed = 0; elapsed < t; elapsed += ETime.FRAME_TIME) {
            main.sr.color = Color.Lerp(Color.black, Color.white, elapsed / t);
            yield return null;
            if (cT.Cancelled) break;
        }
        main.sr.color = Color.white;
        cb();
    }
    private IEnumerator _FadeOut(float t, ICancellee cT, Action cb) {
        for (float elapsed = 0; elapsed < t; elapsed += ETime.FRAME_TIME) {
            main.sr.color = Color.Lerp(Color.black, Color.white, 1 - elapsed / t);
            yield return null;
            if (cT.Cancelled) break;
        }
        main.sr.color = Color.black;
        cb();
    }
    
}
}
