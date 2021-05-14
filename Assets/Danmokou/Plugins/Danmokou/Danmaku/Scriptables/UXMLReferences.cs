using System;
using System.Collections.Generic;
using Danmokou.UI.XML;
using UnityEngine;
using UnityEngine.UIElements;

namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Data/UXML References")]
public class UXMLReferences : ScriptableObject {
    public VisualTreeAsset UIScreen = null!;
    public VisualTreeAsset UINode = null!;
    public VisualTreeAsset TwoTextUINode = null!;
    public VisualTreeAsset OptionLRNode = null!;
    public VisualTreeAsset ComplexOptionLRNode = null!;


    public Dictionary<Type, VisualTreeAsset> TypeMap => new Dictionary<Type, VisualTreeAsset>() {
        {typeof(UIScreen), UIScreen},
        {typeof(UINode), UINode},
        {typeof(TwoLabelUINode), TwoTextUINode},
        {typeof(IOptionNodeLR), OptionLRNode},
        {typeof(IComplexOptionNodeLR), ComplexOptionLRNode}
    };
}
}
