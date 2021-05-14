using Danmokou.Core;
using Danmokou.Scriptables;
using UnityEditor;
using UnityEngine;


[CustomPropertyDrawer(typeof(LocalizedStringReference))]
public class LSReferenceDrawer : PropertyDrawer {
    /// <summary>
    /// Options to display in the popup to select constant or variable.
    /// </summary>
    private readonly string[] popupOptions =
        { "Reference", "Hardcoded"  };

    /// <summary> Cached style to use to draw the popup button. </summary>
    private GUIStyle? popupStyle;

    private static SerializedProperty ChoiceProp(SerializedProperty property) =>
        property.FindPropertyRelative("useReference");
    private static SerializedProperty HardcodedProp(SerializedProperty property) =>
        property.FindPropertyRelative("hardcoded");
    private static SerializedProperty RefProp(SerializedProperty property) =>
        property.FindPropertyRelative("reference");
    
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
        return ChoiceProp(property).boolValue ?
            EditorGUI.GetPropertyHeight(RefProp(property)) :
            EditorGUI.GetPropertyHeight(HardcodedProp(property));
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        if (popupStyle == null) {
            popupStyle = new GUIStyle(GUI.skin.GetStyle("PaneOptions"));
            popupStyle.imagePosition = ImagePosition.ImageOnly;
        }

        label = EditorGUI.BeginProperty(position, label, property);
        position = EditorGUI.PrefixLabel(position, label);

        EditorGUI.BeginChangeCheck();

        // Get properties
        SerializedProperty useReference = ChoiceProp(property);
        SerializedProperty hardcoded = HardcodedProp(property);
        SerializedProperty reference = RefProp(property);

        // Calculate rect for configuration button
        Rect buttonRect = new Rect(position);
        buttonRect.yMin += popupStyle.margin.top;
        buttonRect.width = popupStyle.fixedWidth + popupStyle.margin.right;
        position.xMin = buttonRect.xMax;

        // Store old indent level and set it to 0, the PrefixLabel takes care of it
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        int result = EditorGUI.Popup(buttonRect, useReference.boolValue ? 0 : 1, popupOptions, popupStyle);

        useReference.boolValue = result == 0;

        EditorGUI.PropertyField(position,
            useReference.boolValue ? reference : hardcoded,
            GUIContent.none);

        if (EditorGUI.EndChangeCheck())
            property.serializedObject.ApplyModifiedProperties();

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }
}
