using System;
using System.Collections;
using System.Collections.Generic;
using Danmaku;
using JetBrains.Annotations;
using UnityEngine;

public class AyaPhotoBoard : MonoBehaviour {
    [Serializable]
    public struct PinStrip {
        public Vector2 start;
        public Vector2 end;
        public int maxPins;

        public Vector2 pinLocation(int pin, int inStrip) {
            if (pin < 0 || pin >= maxPins) throw new Exception($"Pin index out of range: {{pin}}/{maxPins}");
            if (maxPins == 1) return start;
            return Vector2.Lerp(start, end, pin / (Math.Min(inStrip, maxPins) - 1f));
        }
    }

    [CanBeNull] private static AyaPhotoBoard main;
    public PinStrip[] strips;
    [CanBeNull] private Vector2[] pinLocations;
    private int nextPin;
    private readonly List<GameObject> pins = new List<GameObject>();
    public GameObject pinPrefab;
    public GameObject defaultPhotoPrefab;
    public Sprite[] pinOptions;
    private static readonly List<AyaPinnedPhoto> bound = new List<AyaPinnedPhoto>();
    public Vector2 pinOffset = new Vector2(0, 0.3f);
    
    private void Awake() {
        main = this;
        bound.Clear();
    }

    public static bool TrySetupPins(int nPins, bool createObjects = true) {
        TearDownAndHide();
        if (main == null) return false;

        main.pinLocations = new Vector2[nPins];
        main.nextPin = 0;
        int si = 0;
        int inStrip = nPins;
        for (int ii = 0, eii = 0; ii < nPins; ++ii, ++eii) {
            while (eii >= main.strips[si].maxPins) {
                eii -= main.strips[si++].maxPins;
                inStrip = nPins - ii;
            }
            main.pinLocations[ii] = main.strips[si].pinLocation(eii, inStrip);
            if (createObjects) {
                var pin = Instantiate(main.pinPrefab);
                main.pins.Add(pin);
                pin.transform.position = main.pinLocations[ii] + main.pinOffset;
                pin.GetComponent<SpriteRenderer>().sprite = main.pinOptions[RNG.GetIntOffFrame(0, main.pinOptions.Length)];
            }
        }
        return true;
    }

    public static void TearDown() {
        if (main != null) {
            main.pinLocations = null;
            main.nextPin = 0;
        }
    }

    public static void TearDownAndHide() {
        foreach (var b in bound) Destroy(b.gameObject);
        bound.Clear();
        if (main != null) {
            TearDown();
            foreach (var p in main.pins) Destroy(p);
            main.pins.Clear();
        }
    }

    public static Vector2? NextPinLoc(AyaPinnedPhoto attach) {
        bound.Add(attach);
        if (main == null) return null;
        return main.pinLocations?.TryN(main.nextPin++);
    }

    public static void ConstructPhotos([CanBeNull] AyaPhoto[] photos, float sizeOverride) {
        if (main == null) return;
        photos = photos ?? new AyaPhoto[0];
        TrySetupPins(photos.Length, false);
        foreach (var p in photos) {
            var pinned = GameObject.Instantiate(main.defaultPhotoPrefab).GetComponent<AyaPinnedPhoto>();
            if (pinned.InitializeAt(p, NextPinLoc(pinned) ??
                                       throw new Exception($"Couldn't find a location to place photo {p.Filename}"))) {
                if (sizeOverride > 0) pinned.SetSize(p, sizeOverride);
            }
        }
    }
}