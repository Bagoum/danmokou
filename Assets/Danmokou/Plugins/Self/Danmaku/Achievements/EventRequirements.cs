using System;
using System.Linq;
using DMK.Core;
using DMK.GameInstance;
using JetBrains.Annotations;

namespace DMK.Achievements {

public class StageCompletedReq : EventRequirement<(string campaign, int stage)> {
    public StageCompletedReq(string campaign, int stage) : 
        base(InstanceRequest.StageCompleted, c => c.campaign == campaign && c.stage == stage) { }
}

public class InstanceRequirement : EventRequirement<InstanceRecord> {
    public InstanceRequirement(Func<InstanceRecord, bool> pred) : 
        base(InstanceRequest.InstanceCompleted, pred) { }
}
public class CompletedInstanceRequirement : InstanceRequirement {
    public CompletedInstanceRequirement(Func<InstanceRecord, bool> pred) : 
        base(i => i.Completed && pred(i)) { }
}

//Note that campaign requirements don't discriminate by campaign; you must pass that discrimination in as pred.
public class NormalCampaignRequirement : CompletedInstanceRequirement {
    public NormalCampaignRequirement(Func<InstanceRecord, bool> pred) : 
        base(i => i.IsAtleastNormalCampaign && pred(i)) { }
}

public class EndingRequirement : NormalCampaignRequirement {
    public EndingRequirement(params string[] endings) : 
        base(i => endings.Contains(i.Ending)) { }
}

public class Normal1CCRequirement : NormalCampaignRequirement {
    public Normal1CCRequirement(Func<InstanceRecord, bool> pred) :
        base(i => i.OneCreditClear && pred(i)) { }
}

public class CustomRequirement : CompletedInstanceRequirement {
    public CustomRequirement(Func<InstanceRecord, bool> pred) : 
        base(i => i.Difficulty.standard == null && pred(i)) { }
}

public class DidntUseMeter1CCReq : Normal1CCRequirement {
    public DidntUseMeter1CCReq(Func<InstanceRecord, bool> pred) :
        base(i => i.MeterFrames == 0 && pred(i)) { }
}

public class Shot1CCReq : Normal1CCRequirement {
    public Shot1CCReq(string shotKey, Func<InstanceRecord, bool> pred) :
        base(i => i.SharedInstanceMetadata.team.players.Any(pc => pc.shot.key == shotKey) && pred(i)) { }
}

}