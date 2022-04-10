using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.Events;
using BagoumLib.Reflection;
using Danmokou.Scriptables;
using Danmokou.Services;
using UnityEngine;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {

public static partial class XMLUtils {
    
    public const string nodeClass = "node";
    public const string noPointerClass = "nopointer";
    public const string disabledClass = "disabled";
    public const string fontUbuntuClass = "font-ubuntu";
    public const string monospaceClass = "monospace";
    public const string noSpacePrefixClass = "nospaceprefix";
    public const string large1Class = "large1";
    public const string large2Class = "large2";
    public const string small1Class = "small1";
    public const string small2Class = "small2";
    public const string small3Class = "small3";
    public const string optionNoKeyClass = "nokey";
    public const string hideClass = "hide";
    public const string descriptorClass = "descriptor";
    public const string centerTextClass = "centertext";
    public static string CheckmarkClass(bool active) => active ? "checked" : "unchecked";

    private static UXMLReferences Prefabs => GameManagement.References.uxmlDefaults;
    public static LString CSpace(this LString s, int space = 12) =>
        LString.Format($"<cspace={space}>{{0}}</cspace>", s);
    public static Length Percent(this float f) => new Length(f, LengthUnit.Percent);

    public static DisplayStyle ToStyle(this bool b) => b ? DisplayStyle.Flex : DisplayStyle.None;

    public static VisualElement CenterElements(this VisualElement root) {
        root.style.justifyContent = Justify.Center;
        root.style.alignItems = Align.Center;
        return root;
    }

    public static VisualElement SetLRMargin(this VisualElement root, float? left, float? right) {
        if (left.Try(out var l))
            root.style.marginLeft = l;
        if (right.Try(out var r))
            root.style.marginRight = r;
        return root;
    }
    public static VisualElement SetPadding(this VisualElement root, float top, float right, float bot, float left) {
        root.style.paddingTop = top;
        root.style.paddingRight = right;
        root.style.paddingBottom = bot;
        root.style.paddingLeft = left;
        return root;
    }

    public static VisualElement SetPadding(this VisualElement root, float padding) =>
        root.SetPadding(padding, padding, padding, padding);

    public static VisualElement ConfigureAbsoluteEmpty(this VisualElement empty, bool pickable = true) {
        empty.SetPadding(0, 0, 0, 0);
        empty.style.position = Position.Absolute;
        var cn = new Length(-50, LengthUnit.Percent);
        empty.style.translate = new StyleTranslate(new Translate(cn, cn, 0));
        empty.pickingMode = pickable ? PickingMode.Position : PickingMode.Ignore;
        return empty;
    }

    public static VisualElement ConfigureLeftTopListeners(this VisualElement n, ICObservable<float> left,
        ICObservable<float> top) {
        left.Subscribe(w => n.style.left = w);
        top.Subscribe(h => n.style.top = h);
        return n;
    }

    public static VisualElement AddVE(this VisualElement root, VisualElement? child) {
        child ??= new VisualElement();
        root.Add(child);
        return child;
    }

    public static VisualElement AddVTA(this VisualElement root, VisualTreeAsset? child) =>
        root.AddVE(child == null ? null : child.CloneTreeWithoutContainer());

    public static VisualElement AddColumn(this VisualElement root) => root.AddVTA(Prefabs.UIScreenColumn);
    public static VisualElement AddScrollColumn(this VisualElement root) {
        var s = root.AddVTA(Prefabs.UIScreenScrollColumn);
        s.Q<ScrollView>().verticalPageSize = 10000;
        return s;
    }

    public static VisualElement AddRow(this VisualElement root) => root.AddVTA(Prefabs.UIScreenRow);
    public static VisualElement AddNodeRow(this VisualElement root) => root.AddVTA(Prefabs.UIScreenRowNoStretch);
    
    public static void ConfigureFloatingImage(VisualElement node, Sprite s) {
        node.style.backgroundImage = new StyleBackground(s);
        //node.style.marginBottom = node.style.marginTop = -s.rect.height / 2;
        //node.style.marginLeft = node.style.marginRight = -s.rect.width / 2;
    }

    public static VisualElement CloneTreeWithoutContainer(this VisualTreeAsset vta) {
        //Unity constructs a templateContainer around the result of any CloneTree operation.
        //This interferes with styling in many cases, since we often want the constructed object
        // to be a direct child of something else (critical in cases featuring position:absolute).
        //To circumvent this, this method clones the tree and then returns the first child in the 
        // template container.
        //Note that a VisualTreeAsset may contain multiple root objects, which is not handled by this.
        // See https://forum.unity.com/threads/why-isnt-templatecontainer-flex-grow-1-by-default.613324/
        var ve = vta.CloneTree();
        //You may need a .Any(c => c.GetType().Name != "UnityBuilderSelectionMarker") 
        if (ve.Children().Skip(1).Any())
            throw new Exception($"Clone tree operation resulted in {ve.childCount} children; expected 1");
        var f = ve.Children().First();
        try {
            foreach (var ss in ve._Field<List<StyleSheet>>("styleSheetList")) {
                if (!f.styleSheets.Contains(ss))
                    f.styleSheets.Add(ss);
            }
        } catch (Exception) {
            //pass
        }
        return f;
    }
}

}
