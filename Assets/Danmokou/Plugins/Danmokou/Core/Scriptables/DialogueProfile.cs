using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Dialogue;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Profile/Dialogue")]
[Obsolete("Replaced by SZYU-based dialogue handling. Will be removed in DMK v9.")]
public class DialogueProfile : ScriptableObject, IDialogueProfile {
    [Serializable]
    public class EmotedSprites2 {
        public DialogueSprite.SpritePiece defaultRoot;
        public Vector2 defaultOffset;
        public DialogueSprite normal;
        public DialogueSprite angry;
        public DialogueSprite happy;
        public DialogueSprite worry;
        public DialogueSprite cry;
        public DialogueSprite surprise;
        public DialogueSprite special;

        public DialogueSprite Find(Emote e) {
            if (e == Emote.ANGRY) return angry;
            if (e == Emote.HAPPY) return happy;
            if (e == Emote.WORRY) return worry;
            if (e == Emote.CRY) return cry;
            if (e == Emote.SURPRISE) return surprise;
            if (e == Emote.SPECIAL) return special;
            return normal;
        }

        public DialogueSprite FillDefault(DialogueSprite ds) {
            if (ds.sprites[0].sprite == null || ds.sprites.Any(s => s.useDefaultOffset)) {
                ds = new DialogueSprite() {
                    sprites = ds.sprites.ToArray()
                };
            }
            if (ds.sprites[0].sprite == null) {
                ds.sprites[0] = defaultRoot;
            }
            for (int ii = 0; ii < ds.sprites.Length; ++ii) {
                if (ds.sprites[ii].useDefaultOffset) ds.sprites[ii].offset = defaultOffset;
            }
            return ds;
        }

        public (bool, DialogueSprite)? FindF(Emote e, bool flip = false) {
            var s = Find(e);
            return (s.sprites.Length == 0) ? ((bool, DialogueSprite)?) null : (flip, FillDefault(s));
        }
    }

    [Serializable]
    public struct EmotedSprites {
        public Sprite? normal;
        public Sprite? angry;
        public Sprite? happy;
        public Sprite? worry;
        public Sprite? cry;
        public Sprite? surprise;
        public Sprite? special;

        public Sprite? Find(Emote e) =>
            e switch {
                Emote.ANGRY => angry,
                Emote.HAPPY => happy,
                Emote.WORRY => worry,
                Emote.CRY => cry,
                Emote.SURPRISE => surprise,
                Emote.SPECIAL => special,
                _ => normal
            };

        public (bool, Sprite)? Find0(Emote e) {
            var s = Find(e);
            return (s == null) ? ((bool, Sprite)?) null : (false, s);
        }

        public (bool, Sprite)? Find1(Emote e) {
            var s = Find(e);
            return (s == null) ? ((bool, Sprite)?) null : (true, s);
        }
    }

    /*
    [ContextMenu("Util-reassign")]
    public void Reassign2() {
        DialogueSprite From1(Sprite s) => s == null ? new DialogueSprite() : 
            new DialogueSprite() {
            sprites = new[] {
                new DialogueSprite.SpritePiece(),
                new DialogueSprite.SpritePiece() {
                    sprite = s
                }
            }
        };
        foreach (var (es1, es2) in new[] {(leftStands, leftStands2), (rightStands, rightStands2)}) {
            es2.normal = From1(es1.normal);
            es2.angry = From1(es1.angry);
            es2.happy = From1(es1.happy);
            es2.worry = From1(es1.worry);
            es2.cry = From1(es1.cry);
            es2.surprise = From1(es1.surprise);
            es2.special = From1(es1.special);
        }
    }*/
    public EmotedSprites2 leftStands2 = null!;
    public EmotedSprites2 rightStands2 = null!;
    public bool flipOnReduce = true;
    public EmotedSprites icons;
    public LocalizedStringReference displayName = null!;
    public string key = null!;
    public string? speakSFX;

    public LString DisplayName => displayName.Value;
    public string? SpeakSFX => speakSFX;

    private static Sprite Cascade(string err, params Sprite?[] options) {
        foreach (var s in options) {
            if (s != null) return s;
        }
        throw new Exception(err);
    }

    public Sprite FindIcon(Emote e) {
        return Cascade("Normal icon not defined", icons.Find(e), icons.Find(Emote.NORMAL));
    }

    public (bool flip, DialogueSprite sprite) FindLeftStand(Emote e) {
        return leftStands2.FindF(e) ?? rightStands2.FindF(e, flipOnReduce) ?? leftStands2.FindF(Emote.NORMAL) ??
            rightStands2.FindF(Emote.NORMAL, flipOnReduce) ??
            throw new Exception($"Could not resolve left stand for emote {e}");
    }

    public (bool flip, DialogueSprite sprite) FindRightStand(Emote e) {
        return rightStands2.FindF(e) ?? leftStands2.FindF(e, flipOnReduce) ?? rightStands2.FindF(Emote.NORMAL) ??
            leftStands2.FindF(Emote.NORMAL, flipOnReduce) ??
            throw new Exception($"Could not resolve right stand for emote {e}");
    }
}
}