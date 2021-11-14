using System;
using System.Collections.Generic;
using Danmokou.UI.XML;
using UnityEngine;
using UnityEngine.UIElements;

namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Data/UXML References")]
public class UXMLReferences : ScriptableObject {
    public VisualTreeAsset UIScreen = null!;
    public VisualTreeAsset UIScreenColumn = null!;
    public VisualTreeAsset UIScreenScrollColumn = null!;
    public VisualTreeAsset UIScreenRow = null!;
    public VisualTreeAsset UIScreenRowNoStretch = null!;
    
    public VisualTreeAsset UINode = null!;
    public VisualTreeAsset TwoTextUINode = null!;
    public VisualTreeAsset OptionLRNode = null!;
    public VisualTreeAsset ComplexOptionLRNode = null!;
    public VisualTreeAsset FloatingNode = null!;
    public VisualTreeAsset AbsoluteTerritory = null!;
    public VisualTreeAsset PopupNode = null!;
    public VisualTreeAsset PopupButton = null!;
    public VisualTreeAsset PureTextNode = null!;
    public VisualTreeAsset TextInputNode = null!;

    public Dictionary<Type, VisualTreeAsset> TypeMap => new() {
        {typeof(UIScreen), UIScreen},
        {typeof(UINode), UINode},
        {typeof(PopupUINode), PopupNode},
        {typeof(UIButton), PopupButton},
        {typeof(TwoLabelUINode), TwoTextUINode},
        {typeof(TextInputNode), TextInputNode},
        {typeof(IOptionNodeLR), OptionLRNode},
        {typeof(IComplexOptionNodeLR), ComplexOptionLRNode}
    };
}
}
