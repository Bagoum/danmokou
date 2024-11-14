using System;
using System.Numerics;
using System.Security.Policy;
using BagoumLib.Culture;
using BagoumLib.Tasks;
using Danmokou.UI.XML;
using Suzunoya.ADV;
using Suzunoya.Entities;

namespace Danmokou.ADV {

/// <summary>
/// A specific object at which evidence can be targeted. 
/// </summary>
public interface IEvidenceTarget {
    public LString? Tooltip { get; }
}

/// <summary>
/// Class that handles presenting evidence to <see cref="ADVEvidenceRequest{E}"/> in two steps:
///  first selecting evidence, and then selecting a target.
/// </summary>
public record EvidenceTargetProxy<E, T>(ADVEvidenceRequest<(E, T)> Request) where E: class where T: IEvidenceTarget {
    public E? NextEvidence { get; set; }

    public UIResult Present(T t) {
        var ev = NextEvidence;
        NextEvidence = null;
        Request.PresentAnyLevel((ev ?? throw new Exception("Target selected without evidence"), t)).Log();
        return new UIResult.ReturnToScreenCaller(2);
    }

    public InteractableEvidenceTargetA<E, T> MakeTarget(T target, IFreeformContainer? container, 
        Vector3 location = default) => 
        new(Request.ADV ?? throw new Exception("EvidenceTargetProxy cannot be used without a bound ADV"), 
            this, target) { XMLContainer = container, Location = location };
}
}