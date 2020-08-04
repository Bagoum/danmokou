using UnityEngine;
using System;

[CreateAssetMenu(menuName = "Basic/Circle")]
public class SOCircle : ScriptableObject, ISerializationCallbackReceiver, ILocationable {
    public Vector2 location;
    public float radius;
    public float largeRadius;
    public float itemCollectRadius;
    public float itemAttractRadius;
    [NonSerialized] public float radius2;
    [NonSerialized] public float lradius2;

    public void OnBeforeSerialize() { }
    public void OnAfterDeserialize() {
        radius2 = radius * radius;
        lradius2 = largeRadius * largeRadius;
    }

    public Vector2 Locate() {
        return location;
    }
}

public interface ILocationable {
    Vector2 Locate();
}