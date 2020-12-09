using System;
using System.Collections;
using System.Collections.Generic;
using DMK.Core;
using UnityEngine;

namespace DMK.UI {
public class MainMenuTutorialWarning : MonoBehaviour {
    private void Awake() {
        if (SaveData.r.TutorialDone) gameObject.SetActive(false);
    }
}
}