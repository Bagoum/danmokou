using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.VN;
using Suzunoya;
using SuzunoyaUnity;
using SuzunoyaUnity.Derived;
using UnityEngine;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace Danmokou.SM {
[Reflect]
public static class TSMReflection {
    
    public enum StandLocation {
        LEFT1,
        LEFT2,
        RIGHT1,
        RIGHT2,
        CENTER
    }
    private static DMKVNState VN => (DMKVNState)ServiceLocator.Find<IVNWrapper>().TrackedVNs.First().vn;
    private static readonly Dictionary<string, Type> characterTypeMap = new Dictionary<string, Type>();
    
    //I don't like putting state here, but it's for backwards compatibility, so whatever
    private static SZYUCharacter? left;
    private static SZYUCharacter? right;
    private static SZYUCharacter? lastSpeaker;
    private static readonly Dictionary<StandLocation, SZYUCharacter> characters = new Dictionary<StandLocation, SZYUCharacter>();
    private static Vector2 GetLocation(StandLocation loc) => loc switch {
        StandLocation.LEFT1 => new Vector2(-5, 0),
        StandLocation.LEFT2 => new Vector2(-2.8f, 0),
        StandLocation.RIGHT1 => new Vector2(3, 0),
        StandLocation.RIGHT2 => new Vector2(0.8f, 0),
        StandLocation.CENTER => Vector2.Zero,
        _ => throw new ArgumentOutOfRangeException(nameof(loc), loc, null)
    };

    static TSMReflection() {
        if (!Application.isPlaying) return;
        var ns = typeof(Reimu).Namespace;
        foreach (var t in typeof(Reimu).Assembly.GetTypes()
            .Where(t => t.Namespace == ns && t.IsSubclassOf(typeof(SZYUCharacter))))
            characterTypeMap[t.Name.ToLower()] = t;
    }

    private static Type GetCharacterType(string name) {
        name = name.ToLower();
        if (!characterTypeMap.ContainsKey(name))
            throw new Exception($"No backwards-compatible character definition for {name}");
        return characterTypeMap[name];
    }

    private static SZYUCharacter CreateCharacter(string name) => 
        VN.Add((Activator.CreateInstance(GetCharacterType(name)) as SZYUCharacter) ?? 
               throw new Exception($"Character {name} couldn't be typecast SZYUCharacter"));

    private static SZYUCharacter FindOrCreateCharacter(string name) =>
        (SZYUCharacter?) VN.FindEntity(GetCharacterType(name)) ?? CreateCharacter(name);

    public static TTaskPattern Re(StateMachine sm) => sm.Start;
    public static TTaskPattern Wait(Synchronizer synchr) {
        var tp = SMReflection.Wait(synchr);
        return smh => tp(smh);
    }

    [Alias("z")]
    public static TTaskPattern Confirm() => smh => VN.SpinUntilConfirm().Task;
    
    public static TTaskPattern Place(StandLocation location, string profile_key) {
        return smh => {
            var chr = FindOrCreateCharacter(profile_key);
            chr.Location.BaseValue = new Vector3(GetLocation(location), 0);
            if (characters.TryGetValue(location, out var existing) && existing != chr)
                existing.Delete();
            characters[location] = chr;
            if (chr.Alpha < 1)
                _ = chr.FadeTo(1, 0.5f).Task;
            return Task.CompletedTask;
        };
    }

    private static TTaskPattern FadeStand(string profile_key, float time, bool fadeIn) => smh => {
        var chr = (SZYUCharacter?) VN.FindEntity(GetCharacterType(profile_key))!;
        return chr.FadeTo(fadeIn ? 1 : 0, time).Task;
    };

    public static TTaskPattern SetStandOpacity(string profile_key, float opacity) => smh => {
        var chr = (SZYUCharacter?) VN.FindEntity(GetCharacterType(profile_key))!;
        chr.Alpha = opacity;
        return Task.CompletedTask;
    };
    public static TTaskPattern FadeStandIn(string profile_key, float time) => FadeStand(profile_key, time, true);
    public static TTaskPattern FadeStandOut(string profile_key, float time) => FadeStand(profile_key, time, false);

    private static TTaskPattern _Text(string text, bool continued) =>
        smh => continued ? 
            lastSpeaker!.AlsoSay(text).Task : 
            lastSpeaker!.Say(text).Task;

    [Alias(".")]
    public static TTaskPattern Text(string text) => _Text(text, false);
    [Alias(".c")]
    public static TTaskPattern ContinuedText(string text) => _Text(text, true);
    [Alias(".cn")]
    public static TTaskPattern ContinuedTextNewline(string text) => _Text("\n" + text, true);
    [Alias(".z")]
    public static TTaskPattern TextConfirm(string text) => async smh => {
        await _Text(text, false)(smh);
        await Confirm()(smh);
    };

    private static string EmoteToString(Emote e) => e switch {
        Emote.NORMAL => "",
        Emote.HAPPY => "happy",
        Emote.ANGRY => "angry",
        Emote.WORRY => "worry",
        Emote.CRY => "cry",
        Emote.SURPRISE => "surprise",
        Emote.SPECIAL => "smug",
        _ => throw new ArgumentOutOfRangeException(nameof(e), e, null)
    };

    private static TTaskPattern _Speak(LR lr, string? profile_key, Emote? emote) {
        return smh => {
            var chr = profile_key == null ? null : FindOrCreateCharacter(profile_key);
            if (lr == LR.LEFT)
                lastSpeaker = (left = chr ?? left) ?? throw new Exception("No left speaker set");
            else
                lastSpeaker = (right = chr ?? right) ?? throw new Exception("No right speaker set");
            if (emote.Try(out var e))
                lastSpeaker!.Emote.Value = EmoteToString(e);
            return Task.CompletedTask;
        };
    }

    [Alias("SL")]
    public static TTaskPattern SpeakL(string profile) => _Speak(LR.LEFT, profile, null);
    [Alias("SR")]
    public static TTaskPattern SpeakR(string profile) => _Speak(LR.RIGHT, profile, null);
    [Alias("SLE")]
    public static TTaskPattern SpeakLE(string profile, Emote e) => _Speak(LR.LEFT, profile, e);
    [Alias("SRE")]
    public static TTaskPattern SpeakRE(string profile, Emote e) => _Speak(LR.RIGHT, profile, e);
    [Alias("SLC")]
    public static TTaskPattern SpeakLC() => _Speak(LR.LEFT, null, null);
    [Alias("SRC")]
    public static TTaskPattern SpeakRC() => _Speak(LR.RIGHT, null, null);
    [Alias("SLCE")]
    public static TTaskPattern SpeakLCE(Emote e) => _Speak(LR.LEFT, null, e);
    [Alias("SRCE")]
    public static TTaskPattern SpeakRCE(Emote e) => _Speak(LR.RIGHT, null, e);
    
    public static TTaskPattern SetEmote(string profile, Emote e) {
        return smh => {
            FindOrCreateCharacter(profile).Emote.Value = EmoteToString(e);
            return Task.CompletedTask;
        };
    }

    public static TTaskPattern Set(StateMachine[] states) => async smh => {
        foreach (var s in states) {
            await s.Start(smh);
            smh.ThrowIfCancelled();
        }
    };

    [Alias("namecard")]
    public static TTaskPattern RawSummon(string prefabName) => smh => {
        BulletManager.RequestRawSummon(prefabName);
        return Task.CompletedTask;
    };
}

}