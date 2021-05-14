using UnityEngine;
using UnityEditor;

public abstract class FivePropertyDrawer : PropertyDrawer {
    protected virtual string arg1 => "nx";
    protected virtual string arg2 => "ny";
    protected virtual string arg3 => "rx";
    protected virtual string arg4 => "ry";
    protected virtual string arg5 => "angle";
    protected virtual float sw => 0.0f;
    protected virtual float w1 => 0.2f;
    protected virtual float w2 => 0.2f;
    protected virtual float w3 => 0.2f;
    protected virtual float w4 => 0.2f;
    protected virtual float w5 => 0.2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        label = EditorGUI.BeginProperty(position, label, property);
        position = EditorGUI.PrefixLabel(position, label);

        EditorGUI.BeginChangeCheck();

        // Get properties
        SerializedProperty nx = property.FindPropertyRelative(arg1);
        SerializedProperty ny = property.FindPropertyRelative(arg2);
        SerializedProperty rx = property.FindPropertyRelative(arg3);
        SerializedProperty ry = property.FindPropertyRelative(arg4);
        SerializedProperty angle = property.FindPropertyRelative(arg5);


        // Store old indent level and set it to 0, the PrefixLabel takes care of it
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        float _sw = position.width * this.sw;
        float pw = position.width - _sw;
        Rect rect1 = new Rect(position.x + _sw, position.y, w1 * pw, position.height);
        Rect rect2 = new Rect(position.x + _sw + w1 * pw, position.y, w2 * pw, position.height);
        Rect rect3 = new Rect(position.x + _sw + (w1+w2) * pw, position.y, w3 * pw, position.height);
        Rect rect4 = new Rect(position.x + _sw + (w1+w2+w3) * pw, position.y, w4 * pw, position.height);
        Rect rect5 = new Rect(position.x + _sw + (1-w5) * pw, position.y, w5 * pw, position.height);

        EditorGUI.PropertyField(rect1, nx, GUIContent.none);
        EditorGUI.PropertyField(rect2, ny, GUIContent.none);
        EditorGUI.PropertyField(rect3, rx, GUIContent.none);
        EditorGUI.PropertyField(rect4, ry, GUIContent.none);
        EditorGUI.PropertyField(rect5, angle, GUIContent.none);

        if (EditorGUI.EndChangeCheck())
            property.serializedObject.ApplyModifiedProperties();

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }
}

[CustomPropertyDrawer(typeof(Danmokou.DMath.MutV2RV2))]
public class V2RV2Drawer : FivePropertyDrawer { }


