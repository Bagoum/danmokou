using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib;
using BagoumLib.DataStructures;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Player;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.UI {
public interface IAyaPhotoBoard {
    /// <summary>
    /// Setup pin spaces and (optionally) pin objects.
    /// <br/>When the returned disposable is disposed, any bound photos and pin objects will be destroyed.
    /// </summary>
    IDisposable SetupPins(int nPins, bool createObjects = true);

    /// <summary>
    /// If this photo board has space for another photo,
    ///  then pass ownership of the given photo to this photo board and provide a location for the photo to be placed,
    ///  otherwise return null.
    /// </summary>
    Vector2? NextPinLoc(AyaPinnedPhoto attach);
    
    /// <summary>
    /// Display several photos without creating pin objects.
    /// <br/>If sizeOverride is positive, then overrides the size of the provided photos.
    /// </summary>
    IDisposable ConstructPhotos(AyaPhoto[]? photos, float sizeOverride);
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
    private readonly List<GameObject> pins = new();
    public GameObject pinPrefab = null!;
    public GameObject defaultPhotoPrefab = null!;
    public Sprite[] pinOptions = null!;
    private readonly List<AyaPinnedPhoto> bound = new();
    public Vector2 pinOffset = new(0, 0.3f);
    private IDisposable? boardToken = null;

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService<IAyaPhotoBoard>(this);
    }

    public IDisposable SetupPins(int nPins, bool createObjects = true) {
        boardToken?.Dispose();
        var pL = pinLocations = new Vector2[nPins];
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
        return boardToken = new JointDisposable(() => {
            if (pL == pinLocations)
                TearDown();
        });
    }

    private void TearDown() {
        pinLocations = null;
        nextPin = 0;
        foreach (var b in bound) Destroy(b.gameObject);
        bound.Clear();
        foreach (var p in pins) Destroy(p);
        pins.Clear();
    }

    public Vector2? NextPinLoc(AyaPinnedPhoto attach) {
        if (pinLocations == null || nextPin >= pinLocations.Length)
            return null;
        bound.Add(attach);
        return pinLocations.TryN(nextPin++);
    }

    public IDisposable ConstructPhotos(AyaPhoto[]? photos, float sizeOverride) {
        photos ??= new AyaPhoto[0];
        var token = SetupPins(photos.Length, false);
        foreach (var p in photos) {
            var pinned = GameObject.Instantiate(defaultPhotoPrefab).GetComponent<AyaPinnedPhoto>();
            if (pinned.InitializeAt(p, NextPinLoc(pinned) ??
                                       throw new Exception($"Couldn't find a location to place photo {p.Filename}"))) {
                if (sizeOverride > 0) pinned.SetSize(p, sizeOverride);
            }
        }
        return token;
    }
}
}