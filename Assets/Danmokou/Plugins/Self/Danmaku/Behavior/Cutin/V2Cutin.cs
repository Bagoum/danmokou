using System;
using System.Collections;
using DMK.Core;
using DMK.DMath;
using DMK.Reflection;
using DMK.Services;
using UnityEngine;
using static DMK.Behavior.Display.CutinHelpers;

namespace DMK.Behavior.Display {
public class V2Cutin : CoroutineRegularUpdater {
    public Transform core;
    public Transform upperText;
    public Transform lowerText;

    private void Awake() {
        upperText.localPosition += new Vector3(-20, 0);
        lowerText.localPosition += new Vector3(20, 0);
        core.localPosition += new Vector3(0, -20);
        RunDroppableRIEnumerator(LetsGo());
    }

    private static TP3 MakeUpperVel =>
        FormattableString.Invariant($"px(lerpt3(1, 1.1, 3.4, 3.5, 18.6, 0.3, 12))").Into<TP3>();
    private static TP3 MakeLowerVel =>
        FormattableString.Invariant($"px(lerpt3(1, 1.1, 3.4, 3.5, -18.6, -0.3, -12))").Into<TP3>();

    private static TP3 MakeCoreVel =>
        FormattableString.Invariant($"if(< t 1.35, zero, py(lerpt3(1.95, 2.05, 3.7, 4, 30, 0.3, 14)))").Into<TP3>();

    private IEnumerator LetsGo() {
        RunDroppableRIEnumerator(Velocity(upperText, MakeUpperVel));
        RunDroppableRIEnumerator(Velocity(lowerText, MakeLowerVel));
        RunDroppableRIEnumerator(Velocity(core, MakeCoreVel));
        float t = 0;
        for (; t < 1.96f; t += ETime.FRAME_TIME) yield return null;
        RunDroppableRIEnumerator(Scale(core, 2f, 0.34f, M.EOutSine));
        for (; t < 1.98f; t += ETime.FRAME_TIME) yield return null;
        RunDroppableRIEnumerator(Rotate(core, new Vector3(0f, 0f, -10f), 3f, M.EOutSine));
        SFXService.Request("x-metal");
    }
}
}