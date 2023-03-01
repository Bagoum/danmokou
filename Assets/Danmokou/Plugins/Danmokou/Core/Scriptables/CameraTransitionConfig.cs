using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Danmokou.Scriptables {

public interface ICameraTransitionConfig {
    Texture FadeToTex { get; }
    TransitionConfig FadeIn { get; }
    TransitionConfig FadeOut { get; }
    Action? OnTransitionComplete { get; }
}

[Serializable]
public struct TransitionConfig {
    public SFXConfig? sfx;
    public Material material;
    public Texture2D transitionTexture;
    public float time;
    public bool reverseFillRatio;
    public bool reverseKeyword;

    public enum FixedType {
        NONE,
        CIRCLEWIPE,
        YWIPE,
        EMPTY
    }

    public FixedType fixedType;

    public float Value(float currTime) {
        var fill = currTime / time;
        return reverseFillRatio ? 1 - fill : fill;
    }

    public TransitionConfig AsSilent {
        get {
            var x = this;
            x.sfx = null;
            return x;
        }
    }
    public TransitionConfig AsInstantaneous {
        get {
            var x = AsSilent;
            x.time = 0;
            return x;
        }
    }
}

[CreateAssetMenu(menuName = "Data/CameraTransitionConfig")]
public class CameraTransitionConfig : ScriptableObject, ICameraTransitionConfig {
    public Texture2D fadeToTex = null!;
    public TransitionConfig fadeIn;
    public TransitionConfig fadeOut;
    
    public Texture FadeToTex => fadeToTex;
    public TransitionConfig FadeIn => fadeIn;
    public TransitionConfig FadeOut => fadeOut;
    public Action? OnTransitionComplete => null;

    public CameraTransitionConfigRec AsRecord => new(fadeToTex, fadeIn, fadeOut);
}

public record CameraTransitionConfigRec
    (Texture FadeToTex, TransitionConfig FadeIn, TransitionConfig FadeOut) : ICameraTransitionConfig {
    public Action? OnTransitionComplete { get; init; }
}

}