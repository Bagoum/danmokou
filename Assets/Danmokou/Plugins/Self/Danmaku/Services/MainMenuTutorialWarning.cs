using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainMenuTutorialWarning : MonoBehaviour {
    private void Awake() {
        if (SaveData.r.TutorialDone) gameObject.SetActive(false);
    }
}