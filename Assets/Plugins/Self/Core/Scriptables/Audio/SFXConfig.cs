using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "SFXConfig")]
public class SFXConfig : ScriptableObject {
    public string defaultName;
    public AudioClip clip;
    public float volume;
    public float timeout = -1f;
    public float Timeout => timeout < 0 ? 0.08f : timeout;
}