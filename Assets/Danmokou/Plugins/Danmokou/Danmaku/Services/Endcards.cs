using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib.Cancellation;
using UnityEngine;
using Danmokou.Behavior;
using Danmokou.Core;

namespace Danmokou.Services {
[Serializable]
public struct Endcard {
    public string name;
    public Sprite image;
}
public class Endcards : CoroutineRegularUpdater {
    private static Endcards main = null!;
    private SpriteRenderer sr = null!;

    public Endcard[] cards = null!;
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
