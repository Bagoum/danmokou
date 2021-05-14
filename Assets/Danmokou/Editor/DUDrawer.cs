using System.Collections.Generic;
using System.Linq;
using Danmokou.Core;
using Danmokou.Graphics.Backgrounds;
using UnityEditor;
using UnityEngine;

public static class DisplayHelpers {
    public static IEnumerable<SerializedProperty> Properties(SerializedProperty property, IEnumerable<string> propNames) =>
        propNames.Select(property.FindPropertyRelative);

    public static float GetPropertyHeight(SerializedProperty property, IEnumerable<string> propNames, GUIContent label) {
        return -EditorGUIUtility.standardVerticalSpacing + Properties(property, propNames)
                   .Sum(sp => EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(sp, true));
    }
}
public abstract class DUDisplayDrawer<T> : PropertyDrawer {
    protected virtual string[] EnumValues { get; } = null!;
    protected virtual string[] EnumDisplayValues => EnumValues;
    protected virtual string propName(int enumIndex) => EnumValues[enumIndex]; //property names same as enum names by default
    protected virtual string enumProperty => "type";
    protected virtual float EnumWidth => 0.2f;
    
    private const float LabelOffset = 0.05f;
    protected virtual string[] OtherPropNames() {
        var enumvals = new HashSet<string>(EnumValues);
        return typeof(T).GetFields().Select(x => x.Name).Where(x => !enumvals.Contains(x) && x != enumProperty).ToArray();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
        SerializedProperty enumVal = property.FindPropertyRelative(enumProperty);
        SerializedProperty duVal = property.FindPropertyRelative(propName(enumVal.enumValueIndex));
        return EditorGUI.GetPropertyHeight(enumVal) + EditorGUI.GetPropertyHeight(duVal, true) + 
               DisplayHelpers.GetPropertyHeight(property, OtherPropNames(), label) + 
               2 * EditorGUIUtility.standardVerticalSpacing;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        label = EditorGUI.BeginProperty(position, label, property);
        var label_position = position;
        position = EditorGUI.PrefixLabel(position, label);

        EditorGUI.BeginChangeCheck();
        SerializedProperty enumVal = property.FindPropertyRelative(enumProperty);
        SerializedProperty duVal = property.FindPropertyRelative(propName(enumVal.enumValueIndex));

        //int indent = EditorGUI.indentLevel;
        //EditorGUI.indentLevel = 0;

        float pw = position.width;
        float lw = label_position.width;
        
        float h1 = EditorGUI.GetPropertyHeight(enumVal);
        Rect rect1 = new Rect(position.x, position.y, pw, h1);
        
        float prevHeight = position.y + h1; //Don't need to subtract vertical spacing since 1 is alreadyt required
        var propnames = OtherPropNames();
        var props = DisplayHelpers.Properties(property, propnames).ToArray();
        for (int ii = 0; ii < props.Length; ++ii) {
            var sp = props[ii];
            prevHeight += EditorGUIUtility.standardVerticalSpacing;
            var myHeight = EditorGUI.GetPropertyHeight(sp, true);
            Rect r = new Rect(label_position.x, prevHeight, lw, myHeight);
            EditorGUI.PropertyField(r, sp, new GUIContent(propnames[ii]));
            prevHeight += myHeight;
        }
        Rect rect2 = new Rect(label_position.x + lw * LabelOffset, prevHeight + EditorGUIUtility.standardVerticalSpacing, 
            lw * (1-LabelOffset), EditorGUI.GetPropertyHeight(duVal, true));

        EditorGUI.PropertyField(rect1, enumVal, GUIContent.none);
        EditorGUI.PropertyField(rect2, duVal, GUIContent.none);

        if (EditorGUI.EndChangeCheck())
            property.serializedObject.ApplyModifiedProperties();

        //EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }
}
[CustomPropertyDrawer(typeof(BackgroundTransition))]
public class BGTransitionDrawer : DUDisplayDrawer<BackgroundTransition> {
    protected override string[] EnumValues { get; } = { "WipeTex", "Wipe1", "WipeFromCenter", "Shatter4", "WipeY" };
}
[CustomPropertyDrawer(typeof(LocatorStrategy))]
public class LocatorStrategyDrawer : DUDisplayDrawer<LocatorStrategy> {
    protected override string[] EnumValues { get; } = { "Source", "Target", "Perimeter" };
}