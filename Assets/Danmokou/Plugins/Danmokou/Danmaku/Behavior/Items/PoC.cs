using BagoumLib;
using Danmokou.Core;
using Danmokou.Player;
using Danmokou.Scriptables;
using Danmokou.Services;
using UnityEngine;

namespace Danmokou.Behavior.Items {
public class PoC : RegularUpdater {
    public LRUD direction;
    private PlayerController? target;
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

    public override void FirstFrame() {
        target = ServiceLocator.FindOrNull<PlayerController>();
    }

    public Vector2 Bound => tr.position;

    private bool IsColliding() {
        if (target != null) {
            var v2 = tr.position;
            var targetLoc = target.Location;
            if (direction == LRUD.UP && targetLoc.y > v2.y) return true;
            else if (direction == LRUD.DOWN && targetLoc.y < v2.y) return true;
            else if (direction == LRUD.LEFT && targetLoc.x < v2.x) return true;
            else if (direction == LRUD.RIGHT && targetLoc.x > v2.x) return true;
            else return false;
        } else return false;
    }
    
    public override void RegularUpdate() {
        Autocollect = IsColliding();
    }
}
}
