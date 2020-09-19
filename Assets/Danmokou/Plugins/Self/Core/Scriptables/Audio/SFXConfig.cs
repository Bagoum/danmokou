using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "SFXConfig")]
public class SFXConfig : ScriptableObject {
    public string defaultName;
    public AudioClip clip;
    public float volume;
    public float timeout = -1f;
    public float Timeout => timeout < 0 ? 0.08f : timeout;
    [Header("Loop Features")]
    public bool loop;
    public float loopTimeCheck;
    public float pitch = 1f;
    public int priority = -1;
    public int Priority => priority < 0 ? 250 : priority;

    public LoopFeature feature;
    public enum LoopFeature {
        NONE = 0,
        /// <summary>
        /// Scale the pitch of this looper by the shotgun and boss low hp variables.
        /// </summary>
        PLAYER_FIRE_HIT = 1
    }
}