using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
using static Danmokou.Services.GameManagement;

namespace Danmokou.UI {
public class ShowVersion : MonoBehaviour {
    private void Awake() {
        GetComponent<TextMeshPro>().text =
            $"DMK {EngineVersion}, {References.gameDefinition.Key} {References.gameDefinition.Version}";
    }
}
}