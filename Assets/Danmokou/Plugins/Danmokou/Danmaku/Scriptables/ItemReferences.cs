using UnityEngine;

namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Data/Item References")]
public class ItemReferences : ScriptableObject {
    public GameObject dropLabel = null!;

    public GameObject lifeItem = null!;
    public GameObject valueItem = null!;
    public GameObject smallValueItem = null!;
    public GameObject pointppItem = null!;
    public GameObject powerItem = null!;
    public GameObject fullPowerItem = null!;
    public GameObject oneUpItem = null!;
    public GameObject gemItem = null!;

    public GameObject powerupShift = null!;
    public GameObject powerupD = null!;
    public GameObject powerupM = null!;
    public GameObject powerupK = null!;
}
}
