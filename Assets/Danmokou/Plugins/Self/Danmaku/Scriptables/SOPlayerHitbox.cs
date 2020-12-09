using UnityEngine;
using System;
using DMK.DMath;
using DMK.Player;

namespace DMK.Scriptables {
[CreateAssetMenu(menuName = "Basic/Player Hitbox")]
public class SOPlayerHitbox : ScriptableObject {
    [field: NonSerialized] public bool Active { get; set; } = false;
    public Vector2 location;
    public float radius;
    public float largeRadius;
    public float itemCollectRadius;
    public float itemAttractRadius;
    [field: NonSerialized] public PlayerHP Player { get; set; }
    public Hitbox Hitbox => new Hitbox(location, radius, largeRadius);
}
}
