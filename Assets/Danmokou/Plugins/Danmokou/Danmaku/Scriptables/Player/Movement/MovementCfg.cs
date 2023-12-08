using Danmokou.Player;
using UnityEngine;

namespace Danmokou.Scriptables {
public interface IMovementCfg {
    PlayerMovement Value { get; }
}


public abstract class MovementCfg: ScriptableObject, IMovementCfg {
    public abstract PlayerMovement Value { get; }
}
}