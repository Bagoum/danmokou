using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {
public interface IUXMLReferences {
    public VisualTreeAsset UIScreenColumn { get; }
    public VisualTreeAsset UIScreenScrollColumn { get; }
    public VisualTreeAsset UIScreenRow { get; }
    public VisualTreeAsset UIScreenRowNoStretch { get; }
    public VisualTreeAsset Popup { get; }
    public VisualTreeAsset Tooltip { get; }
    public VisualTreeAsset ContextMenu { get; }
    public VisualTreeAsset Dropdown { get; }
    public VisualTreeAsset AbsoluteTerritory { get; }
    public VisualTreeAsset PureTextNode { get; }
    public VisualTreeAsset TwoLabelNode { get; }
    public VisualTreeAsset HeaderNode { get; }
    public VisualTreeAsset Cursor { get; }

    public Dictionary<Type, VisualTreeAsset> TypeMap { get; }
}
}