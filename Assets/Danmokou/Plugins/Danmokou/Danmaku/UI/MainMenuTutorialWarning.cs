using System;
using System.Collections;
using System.Collections.Generic;
using Danmokou.Core;
using UnityEngine;

namespace Danmokou.UI {
public class MainMenuTutorialWarning : MonoBehaviour {
    private void Awake() {
        if (SaveData.r.TutorialDone) gameObject.SetActive(false);
    }
}
}