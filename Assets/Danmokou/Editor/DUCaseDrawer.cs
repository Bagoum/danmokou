using System.Collections.Generic;
using System.Linq;
using Danmokou.Core;
using Danmokou.Graphics.Backgrounds;
using NUnit.Framework.Internal;
using UnityEditor;
using UnityEngine;
public abstract class DUCaseDrawer<T> : PropertyDrawer {
    protected virtual string[] PropNames() {
        var flds = typeof(T).GetFields();
        var names = new string[flds.Length];
        for (int ii = 0; ii < flds.Length; ++ii) names[ii] = flds[ii].Name;
        return names;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        DisplayHelpers.GetPropertyHeight(property, PropNames(), label);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        label = EditorGUI.BeginProperty(position, label, property);
        position = EditorGUI.PrefixLabel(position, label);

        EditorGUI.BeginChangeCheck();

        // Store old indent level and set it to 0, the PrefixLabel takes care of it
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        float prevHeight = position.y - EditorGUIUtility.standardVerticalSpacing;
        var propnames = PropNames();
        var props = DisplayHelpers.Properties(property, propnames).ToArray();
        for (int ii = 0; ii < props.Length; ++ii) {
            var sp = props[ii];
            prevHeight += EditorGUIUtility.standardVerticalSpacing;
            var myHeight = EditorGUI.GetPropertyHeight(sp, true);
            Rect r = new Rect(position.x, prevHeight, position.width, myHeight);
            EditorGUI.PropertyField(r, sp, new GUIContent(propnames[ii]));
            prevHeight += myHeight;
        }

        if (EditorGUI.EndChangeCheck())
            property.serializedObject.ApplyModifiedProperties();

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }
}
[CustomPropertyDrawer(typeof(BackgroundTransition.ShatterConfig))]
public class MM0Drawer : DUCaseDrawer<BackgroundTransition.ShatterConfig> { }
[CustomPropertyDrawer(typeof(BackgroundTransition.Wipe1Config))]
public class MM1Drawer : DUCaseDrawer<BackgroundTransition.Wipe1Config> { }
[CustomPropertyDrawer(typeof(BackgroundTransition.WipeTexConfig))]
public class MM2Drawer : DUCaseDrawer<BackgroundTransition.WipeTexConfig> { }
[CustomPropertyDrawer(typeof(BackgroundTransition.WipeFromCenterConfig))]
public class MM3Drawer : DUCaseDrawer<BackgroundTransition.WipeFromCenterConfig> { }
[CustomPropertyDrawer(typeof(BackgroundTransition.WipeYConfig))]
public class MM4Drawer : DUCaseDrawer<BackgroundTransition.WipeYConfig> { }

[CustomPropertyDrawer(typeof(LocatorStrategy.SourceConfig))]
public class LS0Drawer : DUCaseDrawer<LocatorStrategy.SourceConfig> { }
[CustomPropertyDrawer(typeof(LocatorStrategy.TargetConfig))]
public class LS1Drawer : DUCaseDrawer<LocatorStrategy.TargetConfig> { }
[CustomPropertyDrawer(typeof(LocatorStrategy.PerimeterConfig))]
public class LS2Drawer : DUCaseDrawer<LocatorStrategy.PerimeterConfig> { }