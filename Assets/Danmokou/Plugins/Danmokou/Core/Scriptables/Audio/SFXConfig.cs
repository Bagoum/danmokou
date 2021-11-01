using Danmokou.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Audio/SFXConfig")]
public class SFXConfig : ScriptableObject {
    public string defaultName = "";
    public AudioClip clip = null!;
    public float volume;
    public float timeout = -1f;
    public float Timeout => timeout < 0 ? 0.08f : timeout;
    /// <summary>
    /// Looped SFX are always considered
    /// </summary>
    public bool pausable;
    public bool Pausable => pausable || loop;
    /// <summary>
    /// Whether or not the SFX slows down during Witch Time
    /// </summary>
    public bool slowable;
    public float pitch = 1f;
    public float Pitch => pitch * (slowable ? ETime.Slowdown.Value : 1f);
    /// <summary>
    /// True if the SFX requires features that can't be handled by PlayOneShot
    /// </summary>
    public bool RequiresHandling => Mathf.Abs(pitch - 1f) > Mathf.Epsilon || Pausable || slowable;
    [Header("Loop Features")]
    public bool loop;
    public float loopTimeCheck;
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
}
