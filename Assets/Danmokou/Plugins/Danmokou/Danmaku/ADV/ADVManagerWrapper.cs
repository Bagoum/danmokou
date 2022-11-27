using System;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Events;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Scriptables;
using Danmokou.Services;
using Danmokou.VN;
using Newtonsoft.Json;
using Suzunoya.ADV;
using Suzunoya.ControlFlow;
using Suzunoya.Data;
using SuzunoyaUnity;
using UnityEngine;

namespace Danmokou.ADV {

/// <summary>
/// Service that manages the execution of an ADV context.
/// </summary>
public class ADVManagerWrapper : CoroutineRegularUpdater {
    private ADVManager manager = null!;

    protected override void BindListeners() {
        RegisterService(manager = new ADVManager());
        Listen(manager.ADVState, s => Logs.Log($"The running ADV state is now {s}."));
    }
}
}