using UnityEngine;

public class SetMRLayer : MonoBehaviour {
    public string sortingLayer = "UI";
    public int sortingOrder = 5;


    void Start() {
        SetMRLayerf();
    }

    [ContextMenu("Set layer")]
    public void SetMRLayerf() {
        var mr = GetComponent<MeshRenderer>();
        mr.sortingLayerID = SortingLayer.NameToID(sortingLayer);
        mr.sortingOrder = sortingOrder;
    }
}
