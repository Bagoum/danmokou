using Danmokou.Core;
using UnityEditor;
using UnityEngine;


[CustomPropertyDrawer(typeof(MutLString))]
public class LocalizedStringDrawer : PropertyDrawer {
    private readonly string[] popupOptions = {
        "Unified", 
        "Per-Language"
    };

    private const int SecondLangOffset = 20;
    
    private static SerializedProperty EnLang(SerializedProperty property) => 
        property.FindPropertyRelative("en");
    private static (string, SerializedProperty)[] ExtraLangs(SerializedProperty property) => new[] {
        ("JP", property.FindPropertyRelative("jp"))
    };
    
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
        var numShow = 1 + ExtraLangs(property).Length;
        return EditorGUI.GetPropertyHeight(EnLang(property)) * numShow +
               EditorGUIUtility.standardVerticalSpacing * (numShow - 1);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        label = EditorGUI.BeginProperty(position, label, property);
        position = EditorGUI.PrefixLabel(position, label);

        EditorGUI.BeginChangeCheck();

        // Store old indent level and set it to 0, the PrefixLabel takes care of it
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        var enLang = EnLang(property);
        position.height = EditorGUI.GetPropertyHeight(enLang);
        EditorGUI.PropertyField(position, enLang, GUIContent.none);
        
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

        if (EditorGUI.EndChangeCheck())
            property.serializedObject.ApplyModifiedProperties();

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }
}
