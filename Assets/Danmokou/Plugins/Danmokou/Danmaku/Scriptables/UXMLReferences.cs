using System;
using System.Collections.Generic;
using Danmokou.UI.XML;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Data/UXML References")]
public class UXMLReferences : ScriptableObject, IUXMLReferences {
    [field:SerializeField]
    public VisualTreeAsset UIScreen { get; set; } = null!;
    [field:SerializeField]
    public VisualTreeAsset UIScreenColumn { get; set; } = null!;
    [field:SerializeField]
    public VisualTreeAsset UIScreenScrollColumn { get; set; } = null!;
    [field:SerializeField]
    public VisualTreeAsset UIScreenRow { get; set; } = null!;
    [field:SerializeField]
    public VisualTreeAsset UIScreenRowNoStretch { get; set; } = null!;
    [field:SerializeField]
    public VisualTreeAsset Popup { get; set; } = null!;
    [field:SerializeField]
    public VisualTreeAsset Tooltip { get; set; } = null!;
    [field:SerializeField]
    public VisualTreeAsset ContextMenu { get; set; } = null!;
    [field:SerializeField]
    public VisualTreeAsset Dropdown { get; set; } = null!;
    [field:SerializeField]
    
    public VisualTreeAsset UINode { get; set; } = null!;
    [field:SerializeField]
    public VisualTreeAsset OptionsColumnUINode { get; set; } = null!;
    [field:SerializeField]
    public VisualTreeAsset EmptyNode { get; set; } = null!;
    [field:SerializeField]
    public VisualTreeAsset TwoLabelNode { get; set; } = null!;
    [field:SerializeField]
    public VisualTreeAsset OptionLRNode { get; set; } = null!;
    [field:SerializeField]
    public VisualTreeAsset ComplexOptionLRNode { get; set; } = null!;
    [field:SerializeField]
    public VisualTreeAsset FloatingNode { get; set; } = null!;
    [field:SerializeField]
    public VisualTreeAsset AbsoluteTerritory { get; set; } = null!;
    [field:SerializeField]
    public VisualTreeAsset PopupButton { get; set; } = null!;
    [field:SerializeField]
    public VisualTreeAsset PureTextNode { get; set; } = null!;
    [field:SerializeField]
    public VisualTreeAsset TextInputNode { get; set; } = null!;
    [field:SerializeField]
    public VisualTreeAsset HeaderNode { get; set; } = null!;
    [field:SerializeField]
    public VisualTreeAsset SaveLoadNode { get; set; } = null!;
    [field: SerializeField] 
    public VisualTreeAsset Cursor { get; set; } = null!;

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
        {typeof(TwoLabelUINode), TwoLabelNode},
        {typeof(TextInputNode), TextInputNode},
        {typeof(KeyRebindInputNode), TextInputNode},
        {typeof(ILROptionNode), OptionLRNode},
        {typeof(IComplexLROptionNode), ComplexOptionLRNode}
    };
}
}
