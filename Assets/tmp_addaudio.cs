using System.Collections;
using System.Collections.Generic;
using Danmokou.Core;
using Danmokou.Utilities;
using UnityEngine;

public class tmp_addaudio : MonoBehaviour {
    // Start is called before the first frame update
    IEnumerator Start() {
        for (var t = 0f; t < 6; t += Time.deltaTime) {
            yield return null;
        }
        var a = gameObject.AddComponent<AudioSource>();
        Logs.Log("added source");
        var ex = GetComponent<RunBGM>().bgm;
        a.clip = ex.clip;
        a.pitch = ex.pitch;
        a.Play();
    }

}