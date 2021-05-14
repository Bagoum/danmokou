using System;
using Danmokou.Core;
using UnityEngine;

namespace Danmokou.Dialogue {
[Serializable]
public struct DialogueSprite {
    [Serializable]
    public struct SpritePiece {
        public bool useDefaultOffset;
        public Vector2 offset;
        public Sprite sprite;
    }
    public SpritePiece[] sprites;
}
public interface IDialogueProfile {
    Sprite FindIcon(Emote e);
    (bool flip, DialogueSprite sprite) FindLeftStand(Emote e);
    (bool flip, DialogueSprite sprite) FindRightStand(Emote e);
    LocalizedString DisplayName { get; }
    string? SpeakSFX { get; }
}


//Inspector-exposed structs cannot be readonly
[Serializable]
public struct TranslateableDialogue {
    public string name;
    public TranslatedDialogue[] files;
}

[Serializable]
public struct TranslatedDialogue {
    public Locale locale;
    public TextAsset file;
}

}