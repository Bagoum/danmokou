using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using DMath;
using FParser;
using JetBrains.Annotations;
using Microsoft.FSharp.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static FParser.AAParser;
using TC = FParser.AAParser.TextCommand<float, Dialoguer.EventType, string>;
using AR = FParser.AAParser.ArgRef<float, Dialoguer.EventType, string>;

public class Dialoguer : CoroutineRegularUpdater {
    private static Dialoguer main { get; set; }
    public TextMeshProUGUI mainText;
    public TextMeshProUGUI leftSpeaker;
    public TextMeshProUGUI rightSpeaker;
    public Image leftIcon;
    public Image rightIcon;
    public Canvas total;
    private static readonly Dictionary<string, IDialogueProfile> profilesByKey = new Dictionary<string, IDialogueProfile>();

    public SpriteRenderer left1;
    public SpriteRenderer left2;
    public SpriteRenderer right1;
    public SpriteRenderer right2;
    public SpriteRenderer center;
    public SpriteRenderer nextPrompt;

    private void Awake() {
        main = this;
        HideDialogue();
        
        profilesByKey.Clear();
        foreach (var p in GameManagement.References.dialogueProfiles) {
            if (p != null) profilesByKey[p.key] = p;
        }
        
        RunDroppableRIEnumerator(ShowNextOK());
    }

    public static bool WaitingOnConfirm { get; set; } = false;
    private IEnumerator ShowNextOK() {
        Color c = nextPrompt.color;
        float aMult = WaitingOnConfirm ? 0.85f : 0;
        float MultFade(float time) => WaitingOnConfirm ? 0.85f + 0.15f * M.Sin(time * 2f) : 0;
        for (float t = 0;; t += ETime.FRAME_TIME) {
            aMult = Mathf.Lerp(aMult, MultFade(t), 5 * ETime.FRAME_TIME);
            c.a = aMult;
            nextPrompt.color = c;
            yield return null;
        }
    }

    public enum EventType {
        SPEAKER_SFX
    }
    
    private void RequestEvent(EventType ev) {
        if (ev == EventType.SPEAKER_SFX) SFXService.Request(currSpeaker.SpeakSFX);
    }

    private IEnumerator _RunDialogue(TC[] words, CancellationToken cT, Action done, bool continued) {
        float wait_time = 0f;
        List<string> sb = new List<string>();
        int lookaheadStartsAt = 0;
        int ii = 0;
        void AddClearLookahead(string s) {
            sb.RemoveRange(lookaheadStartsAt, sb.Count - lookaheadStartsAt);
            sb.Add(s);
            lookaheadStartsAt = sb.Count;
        }
        if (continued) AddClearLookahead(mainText.text);
        void AddWithLookahead(string s1, string s2, out int i1, out int i2) {
            AddClearLookahead(s1);
            i1 = sb.Count - 1;
            sb.Add("<color=#EDAE4900>");
            i2 = sb.Count;
            sb.Add(s2);
            for (int wi = ii + 1; wi < words.Length; ++wi) {
                var w = words[wi];
                if (w is TC.TSpace sp || w.Equals(TC.TNewline)) break;
                if (w is TC.TTextWait t) sb.Add(t.Item.Item1);
            }
            sb.Add("</color>");
        }
        void AddWait(float t) => wait_time += t * SaveData.s.DialogueWaitMultiplier;
        for (; ii < words.Length; ++ii) {
            for (; wait_time > ETime.FRAME_YIELD; wait_time -= ETime.FRAME_TIME) {
                yield return null;
                if (cT.IsCancellationRequested) {
                    wait_time = -100000f;
                }
            }
            
            var w = words[ii];
            if (w is TC.TTextWait tw) {
                var s = tw.Item.Item1;
                float wait_per = (float)tw.Item.Item2 / s.Length;
                AddWithLookahead(s.Substring(0, 1), s.Substring(1), out int i1, out int i2);
                mainText.text = string.Concat(sb);
                for (int ci = 1; ci < s.Length; ++ci) {
                    for (AddWait(wait_per); wait_time > ETime.FRAME_YIELD; wait_time -= ETime.FRAME_TIME) {
                        yield return null;
                        if (cT.IsCancellationRequested) {
                            wait_time = -100000f;
                        }
                    }
                    sb[i1] = s.Substring(0, ci + 1);
                    sb[i2] = s.Substring(ci + 1);
                    mainText.text = string.Concat(sb);
                }
                AddWait(wait_per);
            } else {
                // ReSharper disable once AccessToModifiedClosure
                //Note: this first null is TC.TTextWait
                w.Resolve(null, s => AddClearLookahead(" "), () => {
                        AddClearLookahead("\n");
                        AddWait(0.5f);
                    }, null, null, 
                    f => AddWait((float)f), AddWait, RequestEvent, SFXService.Request);
            }
            mainText.text = string.Concat(sb);
        }
        done();
    }

    public static IDialogueProfile GetProfile(string key) => profilesByKey.TryGetValue(key, out var v) ?
        v :
        throw new Exception($"No dialogue profile exists by key {key}");

    [CanBeNull] private static IDialogueProfile currLeft;
    [CanBeNull] private static IDialogueProfile currRight;
    private static LR activeSpeakerSide;
    public static IDialogueProfile currSpeaker => (activeSpeakerSide == LR.LEFT) ? currLeft : currRight;
    public static void SetLeftSpeaker([CanBeNull] IDialogueProfile speaker, Emote? emote) {
        currLeft = speaker ?? currLeft ?? throw new Exception("No left speaker is set");
        main.leftSpeaker.text = currLeft.DisplayName;
        if (stands.ContainsKey(currLeft)) {
            var e = emote ?? GetStandEmote(currLeft) ?? throw new Exception("Could not resolve emote");
            UpdateStandEmote(currLeft, e);
        }
        main.leftIcon.sprite = currLeft.FindIcon(emote ?? GetStandEmote(currLeft) ?? Emote.NORMAL);
        SetLeftSpeakerActive();
    }

    public static void SetRightSpeaker([CanBeNull] IDialogueProfile speaker, Emote? emote) {
        currRight = speaker ?? currRight ?? throw new Exception("No right speaker is set");
        main.rightSpeaker.text = currRight.DisplayName;
        if (stands.ContainsKey(currRight)) {
            var e = emote ?? GetStandEmote(currRight) ?? throw new Exception("Could not resolve emote");
            UpdateStandEmote(currRight, e);
        }
        main.rightIcon.sprite = currRight.FindIcon(emote ?? GetStandEmote(currRight) ?? Emote.NORMAL);
        SetRightSpeakerActive();
    }

    private static void SetLeftSpeakerActive() {
        main.leftSpeaker.color = activeSpeaker;
        main.rightSpeaker.color = inactiveSpeaker;
        main.leftIcon.gameObject.SetActive(true);
        main.rightIcon.gameObject.SetActive(false);
        activeSpeakerSide = LR.LEFT;
    }
    private static void SetRightSpeakerActive() {
        main.leftSpeaker.color = inactiveSpeaker;
        main.rightSpeaker.color = activeSpeaker;
        main.leftIcon.gameObject.SetActive(false);
        main.rightIcon.gameObject.SetActive(true);
        activeSpeakerSide = LR.RIGHT;
    }

    private static Emote? GetStandEmote(IDialogueProfile profile) =>
        stands.TryGetValue(profile, out var x) ? x.e : (Emote?)null;
    public static void UpdateStandEmote(IDialogueProfile profile, Emote emote) {
        if (!stands.ContainsKey(profile)) return;
        var location = stands[profile].loc;
        stands[profile] = (location, emote);
        var (flip, sprite) = IsLeft(location) ? profile.FindLeftStand(emote) : profile.FindRightStand(emote);
        var target = main.Target(location);
        target.sprite = sprite;
        var tr = target.transform;
        var ls = tr.localScale;
        ls.x = Mathf.Abs(ls.x) * (flip ? -1 : 1);
        tr.localScale = ls;
    }

    private static IEnumerator _FadeStand(SpriteRenderer sr, float time, bool fadeIn, CancellationToken cT, Action done) {
        var (t0, d) = fadeIn ? (0, ETime.FRAME_TIME) : (time - ETime.FRAME_TIME, -ETime.FRAME_TIME);
        Color c = sr.color;
        for (; 0 <= t0 && t0 < time; t0 += d) {
            c.a = t0 / time;
            sr.color = c;
            yield return null;
            if (cT.IsCancellationRequested) break;
        }
        c.a = fadeIn ? 1 : 0;
        sr.color = c;
        done();
    }

    public static void FadeStand(IDialogueProfile profile, float time, bool fadeIn, CancellationToken cT, Action done) {
        main.RunRIEnumerator(_FadeStand(main.Target(stands[profile].loc), time, fadeIn, cT, done));
    }

    public static void SetOpacity(IDialogueProfile profile, float opacity) {
        var sr = main.Target(stands[profile].loc);
        Color c = sr.color;
        c.a = opacity;
        sr.color = c;
    }

    private static readonly Dictionary<IDialogueProfile, (StandLocation loc, Emote e)> stands = new Dictionary<IDialogueProfile, (StandLocation, Emote)>();
    public static void SetStand(IDialogueProfile profile, StandLocation? loc, Emote? emote) {
        var location = loc ?? (stands.TryGetValue(profile, out var le) ?
            le.loc :
            throw new Exception($"No existing location for profile {profile.DisplayName}"));
        var emt = emote ?? (stands.TryGetValue(profile, out le) ?
            le.e :
            throw new Exception($"No existing emote for profile {profile.DisplayName}"));
        stands[profile] = (location, emt);
        UpdateStandEmote(profile, emt);
    }

    private static readonly Color activeSpeaker = Color.white;
    private static readonly Color inactiveSpeaker = new Color(0.4f, 0.4f, 0.4f, 1f);
    public static void RunDialogue(TC[] words, CancellationToken cT, Action done, bool continued=false) {
        main.RunRIEnumerator(main._RunDialogue(words, cT, done, continued));
    }

    public static void HideDialogue() {
        WaitingOnConfirm = false;
        main.left1.sprite = main.left2.sprite = main.right1.sprite = main.right2.sprite = main.center.sprite = null;
        main.total.enabled = false;
        DialogueActive = false;
    }
    public static void ShowAndResetDialogue() {
        WaitingOnConfirm = false;
        main.leftIcon.gameObject.SetActive(false);
        main.rightIcon.gameObject.SetActive(false);
        currLeft = currRight = null;
        main.leftSpeaker.text = "";
        main.rightSpeaker.text = "";
        main.left1.sprite = main.left2.sprite = main.right1.sprite = main.right2.sprite = main.center.sprite = null;
        main.left1.color = main.left2.color = main.right1.color = main.right2.color = main.center.color = Color.white;
        stands.Clear();
        main.total.enabled = true;
        main.mainText.text = "";
        DialogueActive = true;
    }
    
    public static bool DialogueActive { get; private set; }

    public enum StandLocation {
        LEFT1,
        LEFT2,
        RIGHT1,
        RIGHT2,
        CENTER
    }

    public static bool IsLeft(StandLocation loc) => loc == StandLocation.LEFT1 || loc == StandLocation.LEFT2;

    public SpriteRenderer Target(StandLocation loc) {
        if (loc == StandLocation.LEFT2) return left2;
        if (loc == StandLocation.RIGHT1) return right1;
        if (loc == StandLocation.RIGHT2) return right2;
        if (loc == StandLocation.CENTER) return center;
        return left1;
    }
}