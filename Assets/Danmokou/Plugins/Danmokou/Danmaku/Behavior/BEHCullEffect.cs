using System;
using Danmokou.DMath;
using Danmokou.Scriptables;

namespace Danmokou.Behavior {
public class BEHCullEffect : CoroutineRegularUpdater, IBehaviorEntityDependent {
    public BehaviorEntity beh = null!;
    public EffectStrategy? deathEffect;

    private void Awake() {
        beh.LinkDependentUpdater(this);
    }

    public void OnLinkOrResetValues(bool isLink) {
        EnableUpdates();
    }

    void IBehaviorEntityDependent.Culled(bool allowFinalize, Action done) {
        if (allowFinalize && deathEffect != null) {
            var loc = beh.Location;
            if (LocationHelpers.OnPlayableScreenBy(0.5f, loc))
                deathEffect.Proc(loc, loc, 1f);
        }
        DisableUpdates();
        done();
    }
}
}