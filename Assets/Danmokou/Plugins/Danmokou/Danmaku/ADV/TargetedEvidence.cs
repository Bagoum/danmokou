using System;
using System.Numerics;
using System.Security.Policy;
using BagoumLib.Culture;
using BagoumLib.Tasks;
using Danmokou.UI.XML;
using Suzunoya.ADV;
using Suzunoya.Entities;

namespace Danmokou.ADV {

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
        var _ = Request.PresentAnyLevel((ev ?? throw new Exception("Target selected without evidence"), t))
            .ContinueWithSync();
        return new UIResult.ReturnToScreenCaller(2);
    }

    public InteractableEvidenceTargetA<E, T> MakeTarget(T target, IFixedXMLObjectContainer? container, 
        Vector3 location = default) => 
        new(Request.ADV, this, target) { XMLContainer = container, Location = location };
}
}