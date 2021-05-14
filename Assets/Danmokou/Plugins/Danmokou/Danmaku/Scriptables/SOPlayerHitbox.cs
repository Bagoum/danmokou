using UnityEngine;
using System;
using Danmokou.DMath;
using Danmokou.Player;

namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Basic/Player Hitbox")]
public class SOPlayerHitbox : ScriptableObject {
    [field: NonSerialized] public bool Active { get; set; } = false;
    public Vector2 location;
    public float radius;
    public float largeRadius;
    public float itemCollectRadius;
    public float itemAttractRadius;
    [field: NonSerialized] public PlayerController Player { get; set; } = null!;
    public Hitbox Hitbox => new Hitbox(location, radius, largeRadius);
}
}
