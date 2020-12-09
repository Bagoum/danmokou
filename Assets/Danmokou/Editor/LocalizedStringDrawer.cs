using DMK.Core;
using UnityEditor;
using UnityEngine;


[CustomPropertyDrawer(typeof(LocalizedString))]
public class LocalizedStringDrawer : PropertyDrawer {
    private readonly string[] popupOptions = {
        "Unified", 
        "Per-Language"
    };

    private GUIStyle popupStyle;
    private const int SecondLangOffset = 20;


    private static SerializedProperty EnOnlyProp(SerializedProperty property) => 
        property.FindPropertyRelative("_showEnOnly");
    private static SerializedProperty EnLang(SerializedProperty property) => 
        property.FindPropertyRelative("en");
    private static (string, SerializedProperty)[] ExtraLangs(SerializedProperty property) => new[] {
        ("JP", property.FindPropertyRelative("jp"))
    };
    
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
        var numShow = 1 + (EnOnlyProp(property).boolValue ? 0 : ExtraLangs(property).Length);
        return EditorGUI.GetPropertyHeight(EnLang(property)) * numShow +
               EditorGUIUtility.standardVerticalSpacing * (numShow - 1);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        popupStyle = popupStyle ?? new GUIStyle(GUI.skin.GetStyle("PaneOptions")) {
            imagePosition = ImagePosition.ImageOnly
        };

        label = EditorGUI.BeginProperty(position, label, property);
        position = EditorGUI.PrefixLabel(position, label);

        EditorGUI.BeginChangeCheck();

        SerializedProperty useUnified = EnOnlyProp(property);

        // Calculate rect for configuration button
        Rect buttonRect = new Rect(position);
        buttonRect.yMin += popupStyle.margin.top;
        buttonRect.width = popupStyle.fixedWidth + popupStyle.margin.right;
        position.xMin = buttonRect.xMax;

        // Store old indent level and set it to 0, the PrefixLabel takes care of it
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        int result = EditorGUI.Popup(buttonRect, useUnified.boolValue ? 0 : 1, popupOptions, popupStyle);

        useUnified.boolValue = result == 0;

        var enLang = EnLang(property);
        position.height = EditorGUI.GetPropertyHeight(enLang);
        EditorGUI.PropertyField(position, enLang, GUIContent.none);
        
        if (result == 1) {
            foreach (var (lang, prop) in ExtraLangs(property)) {
                position = new Rect(position.x, position.y + EditorGUI.GetPropertyHeight(prop, true) + EditorGUIUtility.standardVerticalSpacing, 
                    position.width, position.height);
                EditorGUI.PrefixLabel(
                    new Rect(position.x, position.y, SecondLangOffset, position.height), 
                    new GUIContent(lang));
                EditorGUI.PropertyField(
                    new Rect(position.x + SecondLangOffset, position.y, position.width - SecondLangOffset, position.height), 
                    prop, GUIContent.none);
            }
        }

        if (EditorGUI.EndChangeCheck())
            property.serializedObject.ApplyModifiedProperties();

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }
}
