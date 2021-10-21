using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Player;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.UI {
public interface IAyaPhotoBoard {
    void SetupPins(int nPins, bool createObjects = true);
    void TearDown();
    Vector2? NextPinLoc(AyaPinnedPhoto attach);
    void ConstructPhotos(AyaPhoto[]? photos, float sizeOverride);
}

public class AyaPhotoBoard : CoroutineRegularUpdater, IAyaPhotoBoard {
    [Serializable]
    public struct PinStrip {
        public Vector2 start;
        public Vector2 end;
        public int maxPins;

        public Vector2 pinLocation(int pin, int inStrip) {
            if (pin < 0 || pin >= maxPins) throw new Exception($"Pin index out of range: {{pin}}/{maxPins}");
            if (maxPins == 1) return start;
            if (inStrip == 1) return (start + end) * 0.5f;
            return Vector2.Lerp(start, end, pin / (Math.Min(inStrip, maxPins) - 1f));
        }
    }

    public PinStrip[] strips = null!;
    private Vector2[]? pinLocations;
    private int nextPin;
    private readonly List<GameObject> pins = new List<GameObject>();
    public GameObject pinPrefab = null!;
    public GameObject defaultPhotoPrefab = null!;
    public Sprite[] pinOptions = null!;
    private readonly List<AyaPinnedPhoto> bound = new List<AyaPinnedPhoto>();
    public Vector2 pinOffset = new Vector2(0, 0.3f);

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService<IAyaPhotoBoard>(this);
    }


    public void SetupPins(int nPins, bool createObjects = true) {
        TearDown();
        pinLocations = new Vector2[nPins];
        nextPin = 0;
        int si = 0;
        int inStrip = nPins;
        for (int ii = 0, eii = 0; ii < nPins; ++ii, ++eii) {
            while (eii >= strips[si].maxPins) {
                eii -= strips[si++].maxPins;
                inStrip = nPins - ii;
            }
            pinLocations[ii] = strips[si].pinLocation(eii, inStrip);
            if (createObjects) {
                var pin = Instantiate(pinPrefab);
                pins.Add(pin);
                pin.transform.position = pinLocations[ii] + pinOffset;
                pin.GetComponent<SpriteRenderer>().sprite = pinOptions[RNG.GetIntOffFrame(0, pinOptions.Length)];
            }
        }
    }

    public void TearDown() {
        pinLocations = null;
        nextPin = 0;
        foreach (var b in bound) Destroy(b.gameObject);
        bound.Clear();
        foreach (var p in pins) Destroy(p);
        pins.Clear();
    }

    public Vector2? NextPinLoc(AyaPinnedPhoto attach) {
        bound.Add(attach);
        return pinLocations?.TryN(nextPin++);
    }

    public void ConstructPhotos(AyaPhoto[]? photos, float sizeOverride) {
        photos ??= new AyaPhoto[0];
        SetupPins(photos.Length, false);
        foreach (var p in photos) {
            var pinned = GameObject.Instantiate(defaultPhotoPrefab).GetComponent<AyaPinnedPhoto>();
            if (pinned.InitializeAt(p, NextPinLoc(pinned) ??
                                       throw new Exception($"Couldn't find a location to place photo {p.Filename}"))) {
                if (sizeOverride > 0) pinned.SetSize(p, sizeOverride);
            }
        }
    }
}
}