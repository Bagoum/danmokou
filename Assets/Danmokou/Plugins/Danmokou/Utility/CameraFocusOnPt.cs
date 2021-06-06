using System.Collections;
using System.Collections.Generic;
using Danmokou.DMath;
using UnityEngine;

public class CameraFocusOnPt : MonoBehaviour {
    private Camera cam = null!;
    public Transform target = null!;
    public float overTime;
    public float zoom;

    private float baseSize;

    void Start() {
        cam = GetComponent<Camera>();
        baseSize = cam.orthographicSize;
        StartTracking();
    }

    [ContextMenu("Start tracking")]
    public void StartTracking() {
        this.StartCoroutine(FocusOnPt());
    }

    private IEnumerator FocusOnPt() {
        var start = transform.position;
        var startZoom = baseSize / cam.orthographicSize;
        //Let's say an object is at screen location v1 relative to the camera center when zoom is 1.
        //Then, at zoom X, it will be at location x * v1.
        //To retain the screen location v1, the camera center should have position (x-1)/x * v1.

        for (float t = 0; t < overTime; t += Time.deltaTime) {
            var zm = Mathf.Lerp(startZoom, zoom, M.EOutSine(t / overTime));
            cam.orthographicSize = baseSize / zm;
            transform.position = start + (zm - 1) / zm * (target.position - start);
            yield return null;
        }
    }
}