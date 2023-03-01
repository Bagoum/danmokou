using System.Collections;
using System.Collections.Generic;
using BagoumLib;
using Danmokou.Core;
using UnityEngine;

public class tmp_TestLogSkip : MonoBehaviour
{
    // Start is called before the first frame update
    IEnumerator Start() {
        for (var t = 0f; t < 1; t += Time.deltaTime) {
            yield return null;
        }
        Logs.DMKLogs.Log("foo {0} bar {1}", 200, 300, LogLevel.DEBUG1);
        Logs.DMKLogs.Log("hello {0} world {1}", 100, 200, LogLevel.DEBUG2);
    }
}
