using UnityEngine;
using UnityEditor;

public abstract class QuadrupletPropertyDrawer : PropertyDrawer {
    protected virtual string arg1 => "x";
    protected virtual string arg2 => "y";
    protected virtual string arg3 => "angle";
    protected virtual string arg4 => "yscale";

    protected virtual float sw => 0.2f;
    protected virtual float w1 => 0.3f;
    protected virtual float w2 => 0.3f;
    protected virtual float w3 => 0.2f;
    protected virtual float w4 => 0.2f;
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        label = EditorGUI.BeginProperty(position, label, property);
        position = EditorGUI.PrefixLabel(position, label);

        EditorGUI.BeginChangeCheck();

        // Get properties
        SerializedProperty x = property.FindPropertyRelative(arg1);
        SerializedProperty y = property.FindPropertyRelative(arg2);
        SerializedProperty angle = property.FindPropertyRelative(arg3);
        SerializedProperty z = property.FindPropertyRelative(arg4);


        // Store old indent level and set it to 0, the PrefixLabel takes care of it
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        float rw = position.width * this.sw;
        float pw = position.width - rw;
        Rect rect1 = new Rect(position.x + rw, position.y, w1 * pw, position.height);
        Rect rect2 = new Rect(position.x + rw + w1 * pw, position.y, w2 * pw, position.height);
        Rect rect3 = new Rect(position.x + rw + (w1 + w2) * pw, position.y, w3 * pw, position.height);
        Rect rect4 = new Rect(position.x + rw + (1-w4) * pw, position.y, w4 * pw, position.height);

        EditorGUI.PropertyField(rect1, x, GUIContent.none);
        EditorGUI.PropertyField(rect2, y, GUIContent.none);
        EditorGUI.PropertyField(rect3, angle, GUIContent.none);
        EditorGUI.PropertyField(rect4, z, GUIContent.none);

        if (EditorGUI.EndChangeCheck())
            property.serializedObject.ApplyModifiedProperties();

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }
}


[CustomPropertyDrawer(typeof(Danmokou.Danmaku.Descriptors.FrameAnimBullet.BulletAnimSprite))]
public class FrameAnimBulletDrawer : QuadrupletPropertyDrawer {
    protected override string arg1 => "s";
    protected override string arg2 => "time";
    protected override string arg3 => "collisionActive";
    protected override string arg4 => "yscale";
    protected override float sw => 0.1f;
    protected override float w1 => 0.4f;
    protected override float w2 => 0.3f;
    protected override float w3 => 0.05f;
    protected override float w4 => 0.25f;
}

