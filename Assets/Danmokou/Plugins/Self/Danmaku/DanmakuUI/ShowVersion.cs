using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static GameManagement;

public class ShowVersion : MonoBehaviour {
    private void Awake() {
        GetComponent<TextMeshPro>().text =
            $"DMK {EngineVersion}, {References.gameIdentifier} {References.gameVersion}";
    }
}