using UnityEngine;

public interface ITransformHandler {
    Vector2 LocalPosition();
    Vector2 GlobalPosition();
    bool HasParent();
    //Note: if !HasParent, then LocalPosition=GlobalPosition
}