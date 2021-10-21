using Danmokou.Core;
using Danmokou.Scriptables;
using Danmokou.Services;
using UnityEngine;

namespace Danmokou.Behavior.Items {
public class PoC : RegularUpdater {
    public LRUD direction;
    public SOPlayerHitbox target = null!;
    private Transform tr = null!;
    public bool Autocollect { get; private set; }

    private void Awake() {
        tr = transform;
        tr.localPosition += (Vector3) direction.Direction() * (float)GameManagement.Difficulty.pocOffset;
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService(this, new ServiceLocator.ServiceOptions { Unique = true });
    }

    public Vector2 Bound => tr.position;

    private bool IsColliding() {
        var v2 = tr.position; 
        if      (direction == LRUD.UP && target.location.y > v2.y) return true;
        else if (direction == LRUD.DOWN && target.location.y < v2.y) return true;
        else if (direction == LRUD.LEFT && target.location.x < v2.x) return true;
        else if (direction == LRUD.RIGHT && target.location.x > v2.x) return true;
        else return false;
    }
    
    public override void RegularUpdate() {
        Autocollect = IsColliding();
    }
}
}
