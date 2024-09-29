using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using Danmokou.Core;
using Danmokou.Services;
using Danmokou.UI;
using Danmokou.VN;
using Suzunoya.ControlFlow;
using Suzunoya.Data;
using SuzunoyaUnity;
using UnityEngine;

namespace Danmokou.VN {
/// <summary>
/// Loads mimic information from GameManagement.References.SuzunoyaReferences.
/// Also adjusts position to account for UICamera offset.
/// </summary>
public class DMKVNWrapper : VNWrapper {
    public UIManager ui = null!;

    protected override void Awake() {
        var refs = GameManagement.References.suzunoyaReferences;
        if (refs != null) {
            renderGroupMimic = refs.renderGroupMimic;
            entityMimics = refs.entityMimics;
        }
        base.Awake();
    }

    public override ExecutingVN TrackVN(IVNState vn) {
        var evn = base.TrackVN(vn);
        evn.tokens.Add(EngineStateManager.EvState.Subscribe(s => vn.InputAllowed.Value = s == EngineState.RUN));
        return evn;
    }
}
}