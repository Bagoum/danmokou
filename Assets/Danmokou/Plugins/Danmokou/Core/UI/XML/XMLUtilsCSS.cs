using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.Events;
using BagoumLib.Reflection;
using BagoumLib.Tasks;
using Danmokou.Core;
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
    public const string dropdownSelect = "checked";
    public const string dropdownUnselect = "unchecked";
    public const string dropdownTarget = "dropdown-target";
    public static string CheckmarkClass(bool active) => active ? dropdownSelect : dropdownUnselect;

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

    public static class Pivot {
        public static Vector2 TopLeft { get; } = new(0, 1);
        public static Vector2 Top { get; } = new(0.5f, 1);
        public static Vector2 TopRight { get; } = new(1, 1);
        public static Vector2 Center { get; } = new(0.5f, 0.5f);
    }
    

    /// <param name="p">Pivot with (0,0) as bottom left and (1,1) as top right.</param>
    private static Translate ToTranslation(Vector2 p) =>
        new Translate((p.x * -100).Percent(), (p.y * 100 - 100).Percent());
    

    /// <param name="p">Pivot with (0,0) as bottom left and (1,1) as top right.</param>
    private static TransformOrigin ToOrigin(Vector2 p) =>
        new((p.x * 100).Percent(), (100 - 100 * p.y).Percent(), 0f);
    public static VisualElement ConfigureAbsolute(this VisualElement ve, Vector2? pivot = null) {
        var p = pivot ?? Pivot.Center;
        ve.style.position = Position.Absolute;
        ve.style.translate = new StyleTranslate(ToTranslation(p));
        ve.style.transformOrigin = ToOrigin(p);
        return ve;
    }

    public static VisualElement WithAbsolutePosition(this VisualElement ve, Vector2 leftTop) {
        ve.style.left = leftTop.x;
        ve.style.top = leftTop.y;
        return ve;
    }

    public static VisualElement WithAbsolutePositionCentered(this VisualElement ve) {
        ve.style.left = ve.style.top = 50f.Percent();
        return ve;
    }
    
    public static VisualElement WithAbsolutePosition(this VisualElement ve, 
        float? left = null, float? top = null, float? right = null, float? bot = null) {
        if (left is { } l)
            ve.style.left = l;
        if (top is { } t)
            ve.style.top = t;
        if (right is { } r)
            ve.style.right = r;
        if (bot is { } b)
            ve.style.bottom = b;
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


    public static VisualElement SetWidth(this VisualElement n, float w) {
        n.style.width = w;
        n.style.maxWidth = n.style.minWidth = new StyleLength(StyleKeyword.None);
        return n;
    }
    public static VisualElement SetHeight(this VisualElement n, float h) {
        n.style.height = h;
        n.style.maxHeight = n.style.minHeight = new StyleLength(StyleKeyword.None);
        return n;
    }

    public static VisualElement SetWidthHeight(this VisualElement n, Vector2 wh) =>
        n.SetWidth(wh.x).SetHeight(wh.y);

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

    public static VisualElement SetRecursivePickingMode(this VisualElement ve, PickingMode mode) {
        ve.pickingMode = mode;
        foreach (var child in ve.Children())
            child.SetRecursivePickingMode(mode);
        return ve;
    }

    /// <summary>
    /// Reposition an absolute-positioned tooltip relative to a node.
    /// <br/>The relative positioning of the tooltip (eg. top right or top left of the node)
    ///  depends on the CSS classes of the tooltip.
    /// </summary>
    public static void SetTooltipAbsolutePosition(this VisualElement node, VisualElement? tooltip) {
        if (tooltip is null) return;
        var nr = node.worldBound;
        var leftTop = new Vector2(nr.xMax, nr.yMin); //by default, tooltip is above-right
        if (tooltip.ClassListContains("tooltip-above")) {
            leftTop = new(nr.center.x, nr.yMin);
        } else if (tooltip.ClassListContains("tooltip-below")) {
            leftTop = new(nr.center.x, nr.yMax);
        }
        tooltip.WithAbsolutePosition(leftTop);
    }

    /// <summary>
    /// Create a RenderSpace for an absolute-positioned tooltip.
    /// </summary>
    public static UIRenderSpace TooltipRender(this UIScreen s, Action<UIRenderConstructed, VisualElement>? builder = null) =>
        new UIRenderConstructed(s.ScreenRender, XMLUtils.Prefabs.Tooltip, builder)
            .WithTooltipAnim();

    /// <summary>
    /// Instantiate a UIGroup and RenderSpace representing a tooltip, and make it show on the screen under a provided node.
    /// </summary>
    public static T MakeTooltip<T>(this UINode n, Func<UIRenderSpace, T> ttGroup, Action<UIRenderConstructed, VisualElement>? builder = null, bool animateEntry = true) where T : UIGroup {
        var tt = MakeTooltipInner(n.Screen, ttGroup, builder, animateEntry);
        tt.Parent = n.Group;
        n.HTML.SetTooltipAbsolutePosition(tt.Render.HTML);
        return tt;
    }

    /// <summary>
    /// Instantiate a UIGroup and RenderSpace representing a tooltip. The location must be manually set.
    /// </summary>
    public static T MakeTooltip<T>(this XMLDynamicMenu menu, Func<UIRenderSpace, T> ttGroup,
        Action<UIRenderConstructed, VisualElement>? builder = null, bool animateEntry = true) where T : UIGroup {
        var tt = MakeTooltipInner(menu.MainScreen, ttGroup, builder, animateEntry);
        tt.Parent = menu.FreeformGroup;
        return tt;
    }

    private static T MakeTooltipInner<T>(UIScreen s, Func<UIRenderSpace, T> ttGroup,
        Action<UIRenderConstructed, VisualElement>? builder = null, bool animateEntry = true) where T : UIGroup {
        var tt = ttGroup(s.TooltipRender(builder));
        tt.Visibility = new GroupVisibility.UpdateOnLeaveHide(tt);
        tt.Interactable = false;
        tt.DestroyOnLeave = true;
        //can't put this in render.OnBuilt since it needs to run after the tooltip group HTML is constructed
        tt.Render.HTML.SetRecursivePickingMode(PickingMode.Ignore);
        if (!animateEntry)
            tt.Render.IsFirstRender = true;
        _ = tt.EnterGroup()?.ContinueWithSync();
        return tt;
    }

    public static UINode SelectorDropdown(this Selector sel, LString description) =>
        new UINode(description) {
            Prefab = XMLUtils.Prefabs.TwoLabelNode,
            OnConfirm = (n, cs) => PopupUIGroup.CreateDropdown(n, sel)
        }.Bind(new LabelView<(int ct, int first)>(new(sel.SelectedCount, ctf => {
            if (ctf.ct == 0) return "(None)";
            if (ctf.ct > 1) return "(Multiple)";
            return sel.DescribeAt(ctf.first);
        }), "Label2"));

    /// <summary>
    /// Render a visual element as a sprite with its actual coordinates and pivot,
    ///  positioned at the center of the parent object.
    /// </summary>
    public static VisualElement ConfigureParentedFloatingImage(this VisualElement ve, Sprite s) {
        ve.ConfigureAbsolute(s.Pivot())
            .WithAbsolutePositionCentered()
            .SetWidthHeight(UIBuilderRenderer.ToXMLDims(s.Dims()))
            .style.backgroundImage = new(s);
        return ve;
    }
    public static VisualElement ConfigureFloatingImage(this VisualElement ve, Sprite s, Vector2? pivot = null) =>
        ve.ConfigureAbsolute(pivot ?? s.Pivot())
            .ConfigureImage(s);
    
    public static VisualElement ConfigureImage(this VisualElement ve, Sprite s) {
        ve.SetWidthHeight(UIBuilderRenderer.ToXMLDims(s.Dims()))
            .style.backgroundImage = new(s);
        return ve;
    }

    /// <summary>
    /// Move this element to a given index in its parent's child container.
    /// </summary>
    public static VisualElement MoveToIndex(this VisualElement ve, int index) {
        var parent = ve.parent;
        parent.Remove(ve);
        parent.Insert(index, ve);
        return ve;
    }

    public static VisualElement AddTransition(this VisualElement ve, string property, float time,
        EasingMode ease = EasingMode.EaseOut) {
        if (ve.style.transitionProperty.value is null)
            ve.style.transitionProperty = new List<StylePropertyName>();
        if (ve.style.transitionDuration.value is null)
            ve.style.transitionDuration = new List<TimeValue>();
        if (ve.style.transitionTimingFunction.value is null)
            ve.style.transitionTimingFunction = new List<EasingFunction>();
        ve.style.transitionProperty.value.Add(property);
        ve.style.transitionDuration.value.Add(time);
        ve.style.transitionTimingFunction.value.Add(ease);
        return ve;
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
