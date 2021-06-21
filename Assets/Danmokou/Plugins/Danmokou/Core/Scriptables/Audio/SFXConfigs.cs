using Danmokou.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Audio/SFXConfigs")]
public class SFXConfigs : ScriptableObject {
    public SFXConfig[] sfxs = null!;
}
}