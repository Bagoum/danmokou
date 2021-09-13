using System;
using System.Collections;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Reflection;
using Danmokou.Services;
using UnityEngine;
using static Danmokou.Behavior.Display.CutinHelpers;

namespace Danmokou.Behavior.Display {
public class V2Cutin : CoroutineRegularUpdater {
    public Transform core = default!;
    public Transform upperText = default!;
    public Transform lowerText = default!;

    private void Awake() {
        upperText.localPosition += new Vector3(-20, 0);
        lowerText.localPosition += new Vector3(20, 0);
        core.localPosition += new Vector3(0, -20);
        RunDroppableRIEnumerator(LetsGo());
    }

    private static TP3 MakeUpperVel =>
        bpi => new Vector3(M.Lerp3(1, 1.1f, 3.4f, 3.5f, bpi.t, 18.6f, 0.3f, 12f), 0);
    private static TP3 MakeLowerVel =>
        bpi => -1f * MakeUpperVel(bpi);

    private static TP3 MakeCoreVel => bpi => (bpi.t < 1.35f) ?
        Vector3.zero :
        new Vector3(0, M.Lerp3(1.95f, 2.05f, 3.7f, 4f, bpi.t, 30f, 0.3f, 14f));

    private IEnumerator LetsGo() {
        RunDroppableRIEnumerator(Velocity(upperText, MakeUpperVel));
        RunDroppableRIEnumerator(Velocity(lowerText, MakeLowerVel));
        RunDroppableRIEnumerator(Velocity(core, MakeCoreVel));
        float t = 0;
        for (; t < 1.96f; t += ETime.FRAME_TIME) yield return null;
        core.ScaleBy(2f, 0.34f, M.EOutSine).Run(this);
        for (; t < 1.98f; t += ETime.FRAME_TIME) yield return null;
        core.RotateTo(new Vector3(0f, 0f, -10f), 3f, M.EOutSine).Run(this);
        ServiceLocator.SFXService.Request("x-metal");
    }
}
}