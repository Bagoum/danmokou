using System.Collections;
using System.Linq;
using BagoumLib;
using Danmokou.Core;
using Danmokou.DMath;
using UnityEngine;
using static Danmokou.Behavior.Display.CutinHelpers;

namespace Danmokou.Behavior.Display {
public class MirrorBreak : CoroutineRegularUpdater {
    private Transform[] children = null!;
    public Transform controller = null!;
    public float appearDelay = 1.5f;
    public float shatterDelay;
    public Vector3 rotateMin;
    public Vector3 rotateMax;
    public Vector2 scaleRange;
    public float scaleRotateFor;
    public Vector2 fallDelay;
    public Vector3 fallAccel;

    [ContextMenu("Set layers")]
    public void SetLayers() {
        controller.gameObject.SetActive(true);
        foreach (var mr in GetComponentsInChildren<MeshRenderer>()) {
            Debug.Log($"Current sr: {mr.sortingLayerName}");
            mr.sortingLayerID = SortingLayer.NameToID("Walls");
        }
        controller.gameObject.SetActive(false);
    }

    private void Awake() {
        children = controller.childCount.Range().Select(controller.GetChild).ToArray();
        controller.gameObject.SetActive(false);
        RunDroppableRIEnumerator(LetsGo());
    }

    private IEnumerator LetsGo() {
        float t = 0;
        for (; t < appearDelay; t += ETime.FRAME_TIME) yield return null;
        controller.gameObject.SetActive(true);
        for (; t < shatterDelay; t += ETime.FRAME_TIME) yield return null;
        foreach (var c in children) {
            c.RotateTo(RNG.GetV3OffFrame(rotateMin, rotateMax), scaleRotateFor, M.EOutQuad).Run(this);
            c.ScaleBy(RNG.GetFloatOffFrame(scaleRange.x, scaleRange.y), scaleRotateFor, M.EOutQuad).Run(this);
            RunDroppableRIEnumerator(Fall(c, RNG.GetFloatOffFrame(fallDelay.x, fallDelay.y), fallAccel));
        }
    }

    private static IEnumerator Fall(Transform tr, float delay, Vector3 accel) {
        for (float t = 0; t < delay; t += ETime.FRAME_TIME) yield return null;
        var vel = Vector3.zero;
        while (true) {
            vel += accel * ETime.FRAME_TIME;
            tr.localPosition += vel * ETime.FRAME_TIME;
            yield return null;
        }
        // ReSharper disable once IteratorNeverReturns
    }
}
}