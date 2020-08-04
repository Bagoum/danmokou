using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

public interface IDialogueProfile {
    Sprite FindIcon(Emote e);
    (bool flip, Sprite sprite) FindLeftStand(Emote e);
    (bool flip, Sprite sprite) FindRightStand(Emote e);
    string DisplayName { get; }
    string SpeakSFX { get; }
}

[CreateAssetMenu(menuName = "Profile/Dialogue")]
public class DialogueProfile : ScriptableObject, IDialogueProfile {
    [Serializable]
    public struct EmotedSprites {
        [CanBeNull] public Sprite normal;
        [CanBeNull] public Sprite angry;
        [CanBeNull] public Sprite happy;
        [CanBeNull] public Sprite worry;
        [CanBeNull] public Sprite cry;
        [CanBeNull] public Sprite surprise;

        [CanBeNull]
        public Sprite Find(Emote e) {
            if (e == Emote.ANGRY) return angry;
            if (e == Emote.HAPPY) return happy;
            if (e == Emote.WORRY) return worry;
            if (e == Emote.CRY) return cry;
            if (e == Emote.SURPRISE) return surprise;
            return normal;
        }
        public (bool, Sprite)? Find0(Emote e) {
            var s = Find(e);
            return (s == null) ? ((bool, Sprite)?)null : (false, s);
        }
        public (bool, Sprite)? Find1(Emote e) {
            var s = Find(e);
            return (s == null) ? ((bool, Sprite)?)null : (true, s);
        }
    }
    
    public EmotedSprites leftStands;
    public EmotedSprites rightStands;
    public EmotedSprites icons;
    public string displayName;
    public string key;
    public string speakSFX;

    public string DisplayName => displayName;
    public string SpeakSFX => speakSFX;
    
    private static Sprite Cascade(string err, params Sprite[] options) {
        foreach (var s in options) {
            if (s != null) return s;
        }
        throw new Exception(err);
    }
    public Sprite FindIcon(Emote e) {
        return Cascade("Normal icon not defined", icons.Find(e), icons.Find(Emote.NORMAL));
    }

    public (bool flip, Sprite sprite) FindLeftStand(Emote e) {
        return leftStands.Find0(e) ?? rightStands.Find1(e) ?? leftStands.Find0(Emote.NORMAL) ??
            rightStands.Find1(Emote.NORMAL) ?? throw new Exception($"Could not resolve left stand for emote {e}");
    }
    public (bool flip, Sprite sprite) FindRightStand(Emote e) {
        return rightStands.Find0(e) ?? leftStands.Find1(e) ?? rightStands.Find0(Emote.NORMAL) ??
            leftStands.Find1(Emote.NORMAL) ?? throw new Exception($"Could not resolve right stand for emote {e}");
    }
}