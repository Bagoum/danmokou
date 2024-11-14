using Danmokou.Player;
using UnityEngine;

namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Data/Player/Movement/Grid")]
public class GridMovementCfg : MovementCfg {
    public override PlayerMovement Value => new PlayerMovement.Grid(center, unit, min, max, movTime, onMove);
    public Vector2 center;
    public Vector2 unit;
    public Vector2Int min;
    public Vector2Int max;
    public float movTime;
    public SFXConfig? onMove;
}
}