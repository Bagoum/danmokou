using Danmokou.Core;
using Danmokou.Scriptables;
using UnityEngine;
using UnityEditor;

public abstract class TripletPropertyDrawer : PropertyDrawer {
    protected virtual string arg1 => "x";
    protected virtual string arg2 => "y";
    protected virtual string arg3 => "angle";

    protected virtual float sw1 => 0.2f;
    private float sw => showName ? sw1 : 0;
    protected virtual float w1 => 0.4f;
    protected virtual float w2 => 0.3f;
    protected virtual float w3 => 0.2f;

    protected virtual bool showName => true;
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        label = EditorGUI.BeginProperty(position, label, property);
        if(showName) position = EditorGUI.PrefixLabel(position, label);

        EditorGUI.BeginChangeCheck();

        // Get properties
        SerializedProperty x = property.FindPropertyRelative(arg1);
        SerializedProperty y = property.FindPropertyRelative(arg2);
        SerializedProperty angle = property.FindPropertyRelative(arg3);


        // Store old indent level and set it to 0, the PrefixLabel takes care of it
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        float rw = position.width * this.sw1;
        float pw = position.width - rw;
        Rect rect1 = new Rect(position.x + rw, position.y, w1 * pw, position.height);
        Rect rect2 = new Rect(position.x + rw + w1 * pw, position.y, w2 * pw, position.height);
        Rect rect3 = new Rect(position.x + rw + pw - w3 * pw, position.y, w3 * pw, position.height);

        EditorGUI.PropertyField(rect1, x, GUIContent.none);
        EditorGUI.PropertyField(rect2, y, GUIContent.none);
        EditorGUI.PropertyField(rect3, angle, GUIContent.none);

        if (EditorGUI.EndChangeCheck())
            property.serializedObject.ApplyModifiedProperties();

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }
}


[CustomPropertyDrawer(typeof(Danmokou.Danmaku.Descriptors.SimpleBulletEmptyScript.SpriteSpecificGradient))]
public class SpriteVariantDrawer : TripletPropertyDrawer {
    protected override string arg1 => "color";
    protected override string arg2 => "gradient";
    protected override string arg3 => "sprite";
    protected override float w1 => 0.2f;
    protected override float w2 => 0.4f;
    protected override float w3 => 0.4f;
}

[CustomPropertyDrawer(typeof(PalettePoint))]
public class PalettePointDrawer : TripletPropertyDrawer {
    protected override string arg1 => "palette";
    protected override string arg2 => "shade";
    protected override string arg3 => "time";
}


[CustomPropertyDrawer(typeof(Frame))]
public class BEHAnimFrameDrawer : TripletPropertyDrawer {
    protected override string arg1 => "sprite";
    protected override string arg2 => "time";
    protected override string arg3 => "skipLoop";
    protected override float sw1 => 0f;
}

[CustomPropertyDrawer(typeof(Version))]
public class VersionDrawer : TripletPropertyDrawer {
    protected override string arg1 => "major";
    protected override string arg2 => "minor";
    protected override string arg3 => "patch";
    protected override float w1 => 0.35f;
    protected override float w2 => 0.35f;
    protected override float w3 => 0.3f;
    protected override float sw1 => 0f;
}