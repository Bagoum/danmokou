using System;
using DMK.Core;
using DMK.Danmaku;
using DMK.DMath;
using DMK.Graphics;
using DMK.Scriptables;
using JetBrains.Annotations;
using DMK.SM;
using UnityEngine;


namespace DMK.Danmaku.Descriptors {

[Serializable]
public struct DefaultColorizing {
    public GradientModifier? LightMod => 
        FlatLight ? GradientModifier.LIGHTFLAT :
        Light ? GradientModifier.LIGHT : 
        WideLight ? GradientModifier.LIGHTWIDE :
        (GradientModifier?)null;
    public GradientModifier? ColorMod => 
        FlatColor ? GradientModifier.COLORFLAT :
        Color ? GradientModifier.COLOR : 
        WideColor ? GradientModifier.COLORWIDE :
        (GradientModifier?)null;
    public GradientModifier? DarkMod => 
        FlatDark ? GradientModifier.DARKINVFLAT :
        Dark ? GradientModifier.DARKINV : 
        WideDark ? GradientModifier.DARKINVWIDE :
        (GradientModifier?)null;
    public bool FlatLight;
    public bool Light;
    public bool WideLight;
    public bool FlatColor;
    public bool Color;
    public bool WideColor;
    public bool FlatDark;
    public bool Dark;
    public bool WideDark;
    public bool Full;
    public bool Inverted;
    public bool FullInvertedAsLightDark;
    public bool TwoPaletteColorings;
    public bool Any => AnyLight || AnyColor || AnyDark || Full || Inverted;
    public bool AnyLight => FlatLight || Light || WideLight;
    public bool AnyColor => FlatColor || Color || WideColor;
    public bool AnyDark => FlatDark || Dark || WideDark;

    public void AssertValidity() {
        if (FlatLight && Light) throw new Exception("Cannot colorize with light and flatlight together.");
        if (AnyLight && FullInvertedAsLightDark && Full)
            throw new Exception("Cannot colorize with a light color and full assigned to light.");
        if (Dark && FullInvertedAsLightDark && Inverted)
            throw new Exception("Cannot colorize with dark and inverted assigned to dark.");
    }
}

[Serializable]
public struct SimpleBulletEntry {
    public float slideInTime;
    public float fadeInTime;
    public float scaleInTime;
    public float scaleInStart;
}
//Add this script to a bullet prefab, rather than Bullet, to indicate that it is a "simple bullet". 
//Simple bullets will be instantiated as code abstractions rather than game objects. 
//Simple bullets do not support: animation, custom behavior. Also, the sprite must be rotated to face to the right
public class SimpleBulletEmptyScript : MonoBehaviour {
//Inspector-exposed structs cannot be readonly
    [Serializable]
    public struct SpriteSpecificGradient {
        public string color;
        [Tooltip("If null, won't recolor")] [CanBeNull]
        public ColorMap gradient;
        public Sprite sprite;
    }

    [Serializable]
    public struct IdenticalForm {
        public string name;
        public Sprite sprite;
        public int renderOffset;
    }

    public enum DisplacementMethod {
        NORMAL = 0,
        POLAR = 1,
        BIVERTICAL = 2
    }
    [Serializable]
    public struct DisplacementInfo {
        public Texture2D displaceTex;
        public Texture2D displaceMask;
        public float displaceMagnitude;
        public float displaceSpeed;
        public DisplacementMethod displaceMethod;
        public float displaceXMul;

        public void SetOnMaterial(Material material) {
            if (displaceTex != null && displaceMask != null) {
                material.EnableKeyword("FT_DISPLACE");
                material.SetTexture(PropConsts.DisplaceTex, displaceTex);
                material.SetTexture(PropConsts.DisplaceMask, displaceMask);
                material.SetFloat(PropConsts.DisplaceMag, displaceMagnitude);
                material.SetFloat(PropConsts.DisplaceSpd, displaceSpeed);
                material.SetFloat(PropConsts.DisplaceXMul, displaceXMul);
                if (displaceMethod == DisplacementMethod.POLAR) {
                    material.EnableKeyword("FT_DISPLACE_POLAR");
                } else if (displaceMethod == DisplacementMethod.BIVERTICAL) {
                    material.EnableKeyword("FT_DISPLACE_BIVERT");
                }
            }
        }
    }
    [Serializable]
    public struct FrameAnimMetadata {
        [Tooltip("If using a frame-anim sprite, set this to the size of a single sprite. Else set null.")]
        public Sprite sprite0;
        public int numFrames;
        public float framesPerSecond;
    }

    [Tooltip("Is the bullet destroyed on collision?")]
    public bool destructible;
    [Tooltip("Is the bullet destroyed by global deletion effects like bombs?")]
    public bool deletable;
    public ushort grazeEveryFrames = 30;
    public float screenCullRadius = 3f;
    [Header("Rendering Info")] public int renderPriority;
    public DRenderMode renderMode = DRenderMode.NORMAL;
    public SOSBEntry fadeIn;
    public bool rotational;
    [Header("Automatic Colors")] public DefaultColorizing colorizing;
    [Tooltip("Base texture for gradient generation")]
    public Sprite spriteSheet;
    public DisplacementInfo displacement;
    public FrameAnimMetadata frameAnimInfo;
    [Header("Manual Colors")] [Tooltip("Special gradients")]
    public BulletManager.GradientVariant[] gradients;
    [Tooltip("Specific gradients for unique sprites; eg. spellcard")]
    public SpriteSpecificGradient[] spriteSpecificGradients;
    public IdenticalForm[] identicalForms;
    [Header("Cull Bullets Only")] [Tooltip("Set zero if not dummy")]
    public float TTL;
    [Tooltip("[0,x]")] public float timeRandomization;
    [Tooltip("[-x/2,x/2]")] public float rotateRandomization;
    [Header("Enemy Bullet Functionality")]
    public int damage;
    [Header("Player Bullet Functionality")]
    /// <summary>
    /// Non-destructible bullets only.
    /// </summary>
    public int framesPerHit;
}
}