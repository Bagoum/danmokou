using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Data/CameraTransitionConfig")]
public class CameraTransitionConfig : ScriptableObject {
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
    }

    public Texture2D fadeToTex = null!;
    public TransitionConfig fadeIn;
    public TransitionConfig fadeOut;
}
}