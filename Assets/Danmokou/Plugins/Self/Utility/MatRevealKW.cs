using UnityEngine;

public class MatRevealKW : MonoBehaviour {
    public Material material;


    [ContextMenu("Log keywords")]
    public void Reveal() {
        Debug.Log("Revealing keywords for " + material.name);
        foreach (var kw in material.shaderKeywords) {
            Debug.Log(kw);
        }
    }
}
