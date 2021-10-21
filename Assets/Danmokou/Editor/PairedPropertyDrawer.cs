using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Dialogue;
using Danmokou.Pooling;
using Danmokou.Scriptables;
using Danmokou.Services;
using Danmokou.UI;
using Danmokou.UI.XML;
using UnityEngine;
using UnityEditor;


public abstract class PairedPropertyDrawer : PropertyDrawer {
    protected virtual string arg1 => "name";
    protected virtual string arg2 => "file";
    protected virtual float sw1 => 0.2f;
    protected virtual float sw2 => 0f;
    private float sw => showName ? sw2 : sw1;
    protected virtual float w1 => 0.4f;
    protected virtual float w2 => 0.6f;

    protected virtual bool showName => false;
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        label = EditorGUI.BeginProperty(position, label, property);
        if (showName) position = EditorGUI.PrefixLabel(position, label);

        EditorGUI.BeginChangeCheck();

        // Get properties
        SerializedProperty name = property.FindPropertyRelative(arg1);
        SerializedProperty file = property.FindPropertyRelative(arg2);


        // Store old indent level and set it to 0, the PrefixLabel takes care of it
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
/*
        float pw = position.width;
        float sw = pw * (showName ? 0.5f : 0.4f) - 4;
        Rect rect1 = new Rect(position.x + pw * (showName ? 0f : 0.2f), position.y, sw, position.height);
        Rect rect2 = new Rect(position.x + pw - sw, position.y, sw, position.height);
        */
        float rw = position.width * this.sw;
        float pw = position.width - rw;
        Rect rect1 = new Rect(position.x + rw, position.y, w1 * pw, position.height);
        Rect rect2 = new Rect(position.x + rw + w1 * pw, position.y, w2 * pw, position.height);
        

        EditorGUI.PropertyField(rect1, name, GUIContent.none);
        EditorGUI.PropertyField(rect2, file, GUIContent.none);

        if (EditorGUI.EndChangeCheck())
            property.serializedObject.ApplyModifiedProperties();

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }
}


[CustomPropertyDrawer(typeof(SMAsset))]
public class TextAssetDrawer : PairedPropertyDrawer { }

[CustomPropertyDrawer(typeof(DataPrefab))]
public class PrefabDrawer : PairedPropertyDrawer {
    protected override string arg2 => "prefab";
}
[CustomPropertyDrawer(typeof(BulletManager.GradientVariant))]
public class BMGradientContDrawer : PairedPropertyDrawer {
    protected override string arg2 => "gradient";
}
/*
[CustomPropertyDrawer(typeof(AudioTrack.TrackPoint))]
public class AudioTrackPointDrawer : PairedPropertyDrawer {
    protected override string arg2 => "time";
}*/
[CustomPropertyDrawer(typeof(ResourceManager.NamedEffectStrategy))]
public class EffectStrategyDrawer : PairedPropertyDrawer {
    protected override string arg2 => "effect";
}
[CustomPropertyDrawer(typeof(ResourceManager.NamedBackgroundTransition))]
public class NamedBGTransitionDrawer : PairedPropertyDrawer {
    protected override string arg2 => "transition";
}
[CustomPropertyDrawer(typeof(ParticlePooled.ParticleSystemColorConfig.SystemColor))]
public class ParticleSystemColorPairDrawer : PairedPropertyDrawer {
    protected override string arg1 => "system";
    protected override string arg2 => "color";
}
[CustomPropertyDrawer(typeof(Color2))]
public class ColorPairDrawer : PairedPropertyDrawer {
    protected override string arg1 => "color1";
    protected override string arg2 => "color2";
    protected override bool showName => true;
}

[CustomPropertyDrawer(typeof(TranslatedDialogue))]
public class TranslatedDrawer : PairedPropertyDrawer {
    protected override string arg1 => "locale";

    protected override string arg2 => "file";
}

[CustomPropertyDrawer(typeof(PrioritySprite))]
public class PSpriteDrawer : PairedPropertyDrawer {
    protected override string arg1 => "priority";

    protected override string arg2 => "sprite";
}
[CustomPropertyDrawer(typeof(DialogueSprite.SpritePiece))]
public class SpritePieceDrawer : TripletPropertyDrawer {
    protected override string arg1 => "useDefaultOffset";
    protected override string arg2 => "offset";

    protected override string arg3 => "sprite";
    protected override float w1 => 0.1f;
    protected override float w2 => 0.6f;
    protected override float w3 => 0.3f;
    protected override bool showName => false;
}

[CustomPropertyDrawer(typeof(BehaviorEntity.CullableRadius))]
public class CullRadDrawer : PairedPropertyDrawer {
    protected override string arg1 => "cullable";

    protected override string arg2 => "cullRadius";
    protected override float w1 => 0.2f;
    protected override float w2 => 0.8f;
    protected override bool showName => true;
}

[CustomPropertyDrawer(typeof(DifficultyDisplay))]
public class DfcDisplayDrawer : PairedPropertyDrawer {
    protected override string arg1 => "dfc";

    protected override string arg2 => "display";
}
[CustomPropertyDrawer(typeof(OrdinalShot))]
public class OrdinalShotDrawer : PairedPropertyDrawer {
    protected override string arg1 => "ordinal";

    protected override string arg2 => "shot";
}