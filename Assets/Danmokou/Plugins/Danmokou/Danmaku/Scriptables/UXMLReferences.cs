using System;
using System.Collections.Generic;
using Danmokou.UI.XML;
using UnityEngine;
using UnityEngine.UIElements;

namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Data/UXML References")]
public class UXMLReferences : ScriptableObject, IUXMLReferences {
    public VisualTreeAsset UIScreen = null!;
    public VisualTreeAsset UIScreenColumn = null!;
    public VisualTreeAsset UIScreenScrollColumn = null!;
    public VisualTreeAsset UIScreenRow = null!;
    public VisualTreeAsset UIScreenRowNoStretch = null!;
    public VisualTreeAsset Popup = null!;
    
    public VisualTreeAsset UINode = null!;
    public VisualTreeAsset OptionsColumnUINode = null!;
    public VisualTreeAsset EmptyNode = null!;
    public VisualTreeAsset TwoTextUINode = null!;
    public VisualTreeAsset OptionLRNode = null!;
    public VisualTreeAsset ComplexOptionLRNode = null!;
    public VisualTreeAsset FloatingNode = null!;
    public VisualTreeAsset AbsoluteTerritory = null!;
    public VisualTreeAsset PopupButton = null!;
    public VisualTreeAsset PureTextNode = null!;
    public VisualTreeAsset TextInputNode = null!;
    public VisualTreeAsset HeaderNode = null!;
    public VisualTreeAsset SaveLoadNode = null!;

    public Texture2D defaultSaveLoadBG = null!;

    [Header("Markdown renderers")] 
    public VisualTreeAsset MkCodeBlock = null!;
    public VisualTreeAsset MkCodeBlockText = null!;
    public VisualTreeAsset MkEmpty = null!;
    public VisualTreeAsset MkHeader = null!;
    public VisualTreeAsset MkLine = null!;
    public VisualTreeAsset MkLineText = null!;
    public VisualTreeAsset MkLineCode = null!;
    public VisualTreeAsset MkLineLink = null!;
    public VisualTreeAsset MkList = null!;
    public VisualTreeAsset MkListOption = null!;
    public VisualTreeAsset MkParagraph = null!;

    public Dictionary<Type, VisualTreeAsset> TypeMap => new() {
        {typeof(UIScreen), UIScreen},
        {typeof(UINode), UINode},
        {typeof(EmptyNode), EmptyNode},
        {typeof(UIButton), PopupButton},
        {typeof(TwoLabelUINode), TwoTextUINode},
        {typeof(TextInputNode), TextInputNode},
        {typeof(KeyRebindInputNode), TextInputNode},
        {typeof(IOptionNodeLR), OptionLRNode},
        {typeof(IComplexOptionNodeLR), ComplexOptionLRNode}
    };

    VisualTreeAsset IUXMLReferences.UIScreenColumn => UIScreenColumn;
    VisualTreeAsset IUXMLReferences.UIScreenScrollColumn => UIScreenScrollColumn;
    VisualTreeAsset IUXMLReferences.UIScreenRow => UIScreenRow;
    VisualTreeAsset IUXMLReferences.UIScreenRowNoStretch => UIScreenRowNoStretch;
    VisualTreeAsset IUXMLReferences.Popup => Popup;
    VisualTreeAsset IUXMLReferences.AbsoluteTerritory => AbsoluteTerritory;
    VisualTreeAsset IUXMLReferences.PureTextNode => UIScreenRow;
}
}
