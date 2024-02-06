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

public static class XMLUtils {
    static XMLUtils() {
        //In Unity 2023, some internal handling was added where UITK internally queries input from the input system
        // using a *default configuration* for input:
        // https://github.com/Unity-Technologies/UnityCsReference/blob/496f7d6c5c0882f35bb776e96356712d54710033/Modules/InputForUI/Provider/InputManagerProvider.cs#L905
        //This can cause issues if the buttons assigned in the default configuration aren't set up, since
        // the input queries will throw exceptions internally, allocating ~5kb garbage per frame.
        //Setting this flag, which sets 'm_UseInputForUI' to false, prevents UITK from subscribing to the input system:
        // https://github.com/Unity-Technologies/UnityCsReference/blob/496f7d6c5c0882f35bb776e96356712d54710033/Modules/UIElements/Core/DefaultEventSystem.cs#L161
        //If there are no subscriptions, then the interal queries don't run:
        // https://github.com/Unity-Technologies/UnityCsReference/blob/496f7d6c5c0882f35bb776e96356712d54710033/Modules/InputForUI/Provider/EventProvider.cs#L164
        UIToolkitInputConfiguration.SetRuntimeInputBackend(UIToolkitInputBackendOption.LegacyBackend);
    }
    
    public const string highVisClass = "highvis";
    public const string nodeClass = "node";
    public const string noPointerClass = "nopointer";
    public const string disabledClass = "disabled";
    public const string fontUbuntuClass = "font-ubuntu";
    public const string fontControlsClass = "font-controls";
    public const string fontBiolinumClass = "font-biolinum";
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

    public static IUXMLReferences Prefabs => ServiceLocator.Find<IUXMLReferences>();
    public static LString CSpace(this LString s, int space = 12) =>
        LString.Format($"<cspace={space}>{{0}}</cspace>", s);
    public static Length Percent(this float f) => new Length(f, LengthUnit.Percent);

    public static StyleLength ToLength(this float? f) =>
        f.Try(out var l) ? l : new StyleLength(StyleKeyword.Initial);

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

    public static VisualElement SetBorder(this VisualElement root, Color color, float width) {
        var s = root.style;
        s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = s.borderTopWidth = width;
        s.borderBottomColor = s.borderLeftColor = s.borderRightColor = s.borderTopColor = color;
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

    public enum Pivot {
        TopLeft,
        Top,
        TopRight,
        Center,
    }

    private static Translate ToTranslation(Pivot p) => p switch {
        Pivot.TopLeft => Translate.None(),
        Pivot.Top => new Translate((-50f).Percent(), 0f, 0),
        Pivot.TopRight => new Translate((-100f).Percent(), 0f, 0),
        _ => new Translate((-50f).Percent(), (-50f).Percent(), 0)
    };
    private static TransformOrigin ToOrigin(Pivot p) => p switch {
        Pivot.TopLeft => new TransformOrigin(0f.Percent(), 0f.Percent(), 0f),
        Pivot.Top => new TransformOrigin(50f.Percent(), 0f.Percent(), 0f),
        Pivot.TopRight => new TransformOrigin(100f.Percent(), 0f.Percent(), 0f),
        _ => TransformOrigin.Initial()
    };
    public static VisualElement ConfigureAbsolute(this VisualElement ve, Pivot pivot = Pivot.Center) {
        ve.style.position = Position.Absolute;
        ve.style.translate = new StyleTranslate(ToTranslation(pivot));
        ve.style.transformOrigin = ToOrigin(pivot);
        return ve;
    }

    public static VisualElement ConfigureEmpty(this VisualElement empty, bool pickable = true) {
        empty.SetPadding(0, 0, 0, 0);
        empty.pickingMode = pickable ? PickingMode.Position : PickingMode.Ignore;
        return empty;
    }

    public static VisualElement ConfigureFixedXMLPositions(this VisualElement n, IFixedXMLObject source) =>
            n.ConfigureLeftTopListeners(source.Left, source.Top)
             .ConfigureWidthHeightListeners(source.Width, source.Height);

    public static VisualElement ConfigureLeftTopListeners(this VisualElement n, ICObservable<float> left,
        ICObservable<float> top) {
        left.Subscribe(w => n.style.left = w);
        top.Subscribe(h => n.style.top = h);
        return n;
    }
    
    public static VisualElement ConfigureWidthHeightListeners(this VisualElement n, ICObservable<float?> width,
        ICObservable<float?> height) {
        width.Subscribe(w => n.style.width = w.ToLength());
        height.Subscribe(h => n.style.height = h.ToLength());
        return n;
    }

    public static VisualElement AddVE(this VisualElement root, VisualElement? child) {
        child ??= new VisualElement();
        root.Add(child);
        return child;
    }

    public static VisualElement AddVTA(this VisualElement root, VisualTreeAsset? child) =>
        root.AddVE(child == null ? null : child.CloneTreeNoContainer());

    public static VisualElement AddColumn(this VisualElement root) => root.AddVTA(Prefabs.UIScreenColumn);
    public static VisualElement AddScrollColumn(this VisualElement root) {
        var s = root.AddVTA(Prefabs.UIScreenScrollColumn);
        s.Q<ScrollView>().verticalPageSize = 1000;
        s.Q<ScrollView>().mouseWheelScrollSize = 1000;
        return s;
    }
    public static VisualElement AddZeroPaddingScrollColumn(this VisualElement root) {
        var s = root.AddVTA(Prefabs.UIScreenScrollColumn);
        s.Q<ScrollView>().verticalPageSize = 1000;
        s.Q<ScrollView>().mouseWheelScrollSize = 1000;
        s.style.width = new Length(100, LengthUnit.Percent);
        var scrollBox = s.Q(null, "unity-scroll-view__content-viewport");
        scrollBox.style.paddingLeft = 0;
        scrollBox.style.paddingRight = 0;
        return s;
    }

    public static VisualElement AddRow(this VisualElement root) => root.AddVTA(Prefabs.UIScreenRow);
    public static VisualElement AddNodeRow(this VisualElement root) => root.AddVTA(Prefabs.UIScreenRowNoStretch);
    
    public static void ConfigureFloatingImage(VisualElement node, Sprite s) {
        node.style.backgroundImage = new StyleBackground(s);
        //node.style.marginBottom = node.style.marginTop = -s.rect.height / 2;
        //node.style.marginLeft = node.style.marginRight = -s.rect.width / 2;
    }

    public static VisualElement CloneTreeNoContainer(this VisualTreeAsset vta) {
        //Unity constructs a templateContainer around the result of any CloneTree operation.
        //This interferes with styling in many cases, since we often want the constructed object
        // to be a direct child of something else (critical in cases featuring position:absolute).
        //To circumvent this, this method clones the tree and then returns the first child in the 
        // template container.
        //Note that a VisualTreeAsset may contain multiple root objects, which is not handled by this.
        // See https://forum.unity.com/threads/why-isnt-templatecontainer-flex-grow-1-by-default.613324/
        var ve = vta.Instantiate();
        //You may need a .Any(c => c.GetType().Name != "UnityBuilderSelectionMarker") 
        if (ve.Children().Skip(1).Any())
            throw new Exception($"Clone tree operation resulted in {ve.childCount} children; expected 1");
        var f = ve.Children().First();
        try {
            for (int ii = 0; ii < ve.styleSheets.count; ++ii) {
                var ss = ve.styleSheets[ii];
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
