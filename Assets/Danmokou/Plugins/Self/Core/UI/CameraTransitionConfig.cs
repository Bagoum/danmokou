using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/CameraTransitionConfig")]
public class CameraTransitionConfig : ScriptableObject {
    [Serializable]
    public struct TransitionConfig {
        public Texture2D transitionTexture;
        public float time;
        public bool reverse;

        public enum FixedType {
            NOT,
            CIRCLEWIPE,
            YWIPE,
            EMPTY
        }

        public FixedType fixedType;

        public float Value(float currTime) {
            return currTime / time;
        }
    }
    public Texture2D fadeToTex;
    public TransitionConfig fadeIn;
    public TransitionConfig fadeOut;
    
}
