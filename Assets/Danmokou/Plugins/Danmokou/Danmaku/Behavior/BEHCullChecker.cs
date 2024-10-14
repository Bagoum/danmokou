using System;
using Danmokou.Core;
using Danmokou.Danmaku.Options;
using Danmokou.DMath;
using Danmokou.Scriptables;

namespace Danmokou.Behavior {
public class BEHCullChecker: CoroutineRegularUpdater, IBehaviorEntityDependent {
    private const float FIRST_CULLCHECK_TIME = 2;
    
    public BehaviorEntity beh = null!;
    //Laser is not screen-cullable, but can have `delete` set
    //Pather does not have this component; it uses custom cull handling
    public bool cullable = true;
    public SOFloat? cullRadius;
    
    public float ScreenCullRadius => (cullRadius == null) ?  4f : cullRadius.value;
    private const int checkCullEvery = 120;
    private int cullCtr = 0;
    private Pred? delete;

    private void Awake() {
        beh.LinkDependentUpdater(this);
    }
    
    public void OnLinkOrResetValues(bool isLink) {
        EnableUpdates();
    }

    public void Initialized(RealizedBehOptions? options) {
        delete = options?.delete;
    }

    public override void RegularUpdate() {
        base.RegularUpdate();
        if (CullCheck())
            beh.InvokeCull();
    }

    public override int UpdatePriority => UpdatePriorities.BEH + 1;

    private bool CullCheck() {
        var bpi = beh.rBPI;
        if (delete?.Invoke(bpi) == true) {
            return true;
        } else if (cullable) {
            if (cullCtr == 0 && beh.Style.CameraCullable.Value && bpi.t > FIRST_CULLCHECK_TIME &&
                     LocationHelpers.OffPlayableScreenBy(ScreenCullRadius, bpi.loc)) {
                return true;
            } else {
                cullCtr = (cullCtr + 1) % checkCullEvery;
            }
        }
        return false;
    }

    void IBehaviorEntityDependent.Culled(bool allowFinalize, Action done) {
        DisableUpdates();
        done();
    }
}
}