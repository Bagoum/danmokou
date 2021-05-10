using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DMK.Behavior;
using DMK.Core;
using DMK.Dialogue;
using DMK.DMath;
using DMK.Services;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ParserCS.AAParser;
using DUContents = DMK.Core.DU<float, string, DMK.Dialogue.Dialoguer.EventType>;

namespace DMK.Dialogue {

public readonly struct DialogueObject {
    //Cases: 
    // wait<float>
    // sfx<string>
    // event<Dialoguer.EventType>
    public readonly DUContents contents;
    public DialogueObject(DUContents contents) {
        this.contents = contents;
    }

    public static DialogueObject Wait(float f) => new DialogueObject(new DUContents(f));
    public static DialogueObject SFX(string s) => new DialogueObject(new DUContents(s));
    public static DialogueObject Event(Dialoguer.EventType e) => new DialogueObject(new DUContents(e));
}
public class Dialoguer : CoroutineRegularUpdater {
    private static Dialoguer main { get; set; } = null!;
    public TextMeshProUGUI mainText = null!;
    public TextMeshProUGUI leftSpeaker = null!;
    public TextMeshProUGUI rightSpeaker = null!;
    public Image leftIcon = null!;
    public Image rightIcon = null!;
    public GameObject total = null!;

    public SpriteRenderer left1 = null!;
    public SpriteRenderer left2 = null!;
    public SpriteRenderer right1 = null!;
    public SpriteRenderer right2 = null!;
    public SpriteRenderer center = null!;
    public SpriteRenderer nextPrompt = null!;

    private readonly Dictionary<StandLocation, PiecewiseRender> renders =
        new Dictionary<StandLocation, PiecewiseRender>();

    private PiecewiseRender NewPiecewise(StandLocation rootSR, DialogueSprite sprite) {
        if (renders.TryGetValue(rootSR, out var pr)) pr.DestroySpawned();
        else renders[rootSR] = pr = new PiecewiseRender();
        pr.Restructure(Target(rootSR), sprite);
        return pr;
    }

    private static IEnumerator Dim(PiecewiseRender pr, Color color, float bop, float overT = 0.2f) {
        if (pr.cT?.Cancelled == true) yield break;
        Color baseColor = pr.lastColor;
        float baseBop = pr.lastBop;
        for (float t = 0; t < overT; t += ETime.FRAME_TIME) {
            pr.lastColor = Color.Lerp(baseColor, color, t / overT).WithA(pr.lastColor.a);
            float nextBop = baseBop + M.EOutSine(t / overT) * (bop - baseBop);
            foreach (var sr in pr.spriteRenders) {
                sr.color = pr.lastColor;
                var p = sr.transform.localPosition;
                p.y += (nextBop - pr.lastBop);
                sr.transform.localPosition = p;
            }
            pr.lastBop = nextBop;
            if (pr.cT?.Cancelled == true) yield break;
            yield return null;
        }
        pr.lastColor = color.WithA(pr.lastColor.a);
    }

    private const float BOPHEIGHT = 0.2f;

    private void DimAll(StandLocation? except, Color dimColor, Color normalColor) {
        foreach (var sr in renders.Keys.ToArray()) {
            renders[sr].cT?.Cancel();
            renders[sr].cT = new Cancellable();
            var isExcept = sr == except;
            RunDroppableRIEnumerator(Dim(renders[sr],
                isExcept ? normalColor : dimColor,
                isExcept ? BOPHEIGHT : 0f));
        }
    }

    private static Color Gray(float s) => new Color(s, s, s, 1);
    private static readonly Color DimColor = Gray(0.7f);
    private void DimAll(StandLocation? except) => DimAll(except, DimColor, Gray(1f));

    private class PiecewiseRender {
        public SpriteRenderer[] spriteRenders = new SpriteRenderer[0];
        public Cancellable? cT;
        public Color lastColor = DimColor;
        public float lastBop;

        public void ApplyColor(Color c) {
            lastColor = c;
            foreach (var sr in spriteRenders) {
                sr.color = c;
            }
        }

        public void Restructure(SpriteRenderer rootSR, DialogueSprite sprite) {
            var tr = rootSR.transform;
            var rootOrder = rootSR.sortingOrder;
            var layer = rootSR.gameObject.layer;
            spriteRenders = sprite.sprites.Where(x => x.sprite != null).Select(s => {
                var go = new GameObject {layer = layer};
                go.transform.SetParent(tr, false);
                go.transform.localPosition = s.offset * (1 / s.sprite.pixelsPerUnit) + new Vector2(0f, lastBop);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = s.sprite;
                sr.sortingOrder = ++rootOrder;
                sr.maskInteraction = rootSR.maskInteraction;
                sr.sortingLayerID = rootSR.sortingLayerID;
                return sr;
            }).ToArray();
        }

        public void DestroySpawned() {
            cT?.Cancel();
            cT = null;
            foreach (var sr in spriteRenders) {
                if (sr != null) UnityEngine.Object.Destroy(sr.gameObject);
            }
            spriteRenders = new SpriteRenderer[0];
        }
    }

    private void Awake() {
        main = this;
        HideDialogue();

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
        // ReSharper disable once IteratorNeverReturns
    }

    public enum EventType {
        SPEAKER_SFX
    }

    private void RequestEvent(EventType ev) {
        if (ev == EventType.SPEAKER_SFX) DependencyInjection.SFXService.Request(currSpeaker?.SpeakSFX);
    }

    private IEnumerator _RunDialogue(TextCommand<DialogueObject>[] words, ICancellee cT, Action done, bool continued) {
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
                if (w.type == TextCommand.Type.Space || w.type == TextCommand.Type.Newline) break;
                if (w.type == TextCommand.Type.TextWait) sb.Add(w.stringVal);
            }
            sb.Add("</color>");
        }
        void AddWait(float t) => wait_time += t * SaveData.s.DialogueWaitMultiplier;
        for (; ii < words.Length; ++ii) {
            for (; wait_time > ETime.FRAME_YIELD; wait_time -= ETime.FRAME_TIME) {
                yield return null;
                if (cT.Cancelled) {
                    wait_time = -100000f;
                }
            }

            var w = words[ii];
            if (w.type == TextCommand.Type.TextWait) {
                var s = w.stringVal;
                float wait_per = (float) w.wait / s.Length;
                AddWithLookahead(s.Substring(0, 1), s.Substring(1), out int i1, out int i2);
                mainText.text = string.Concat(sb);
                for (int ci = 1; ci < s.Length; ++ci) {
                    for (AddWait(wait_per); wait_time > ETime.FRAME_YIELD; wait_time -= ETime.FRAME_TIME) {
                        yield return null;
                        if (cT.Cancelled) {
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
                w.Resolve(null!, s => AddClearLookahead(" "), () => {
                        AddClearLookahead("\n");
                        AddWait(0.5f);
                    }, null!, null!,
                    AddWait, 
                    dobj => dobj.contents.Resolve(AddWait, DependencyInjection.SFXService.Request, RequestEvent));
            }
            mainText.text = string.Concat(sb);
        }
        done();
    }

    public static IDialogueProfile GetProfile(string key) =>
        GameManagement.References.dialogueProfiles.FirstOrDefault(p => p.key == key) ??
        throw new Exception($"No dialogue profile exists by key {key}");

    private static IDialogueProfile? currLeft;
    private static IDialogueProfile? currRight;
    private static LR activeSpeakerSide;
    public static IDialogueProfile? currSpeaker => (activeSpeakerSide == LR.LEFT) ? currLeft : currRight;

    public static void SetLeftSpeaker(IDialogueProfile? speaker, Emote? emote) {
        currLeft = speaker ?? currLeft ?? throw new Exception("No left speaker is set");
        main.leftSpeaker.text = currLeft.DisplayName;
        if (stands.ContainsKey(currLeft)) {
            var e = emote ?? GetStandEmote(currLeft) ?? throw new Exception("Could not resolve emote");
            main.DimAll(UpdateStandEmote(currLeft, e));
        } else main.DimAll(null);
        main.leftIcon.sprite = currLeft.FindIcon(emote ?? GetStandEmote(currLeft) ?? Emote.NORMAL);
        SetLeftSpeakerActive();
    }

    public static void SetRightSpeaker(IDialogueProfile? speaker, Emote? emote) {
        currRight = speaker ?? currRight ?? throw new Exception("No right speaker is set");
        main.rightSpeaker.text = currRight.DisplayName;
        if (stands.ContainsKey(currRight)) {
            var e = emote ?? GetStandEmote(currRight) ?? throw new Exception("Could not resolve emote");
            main.DimAll(UpdateStandEmote(currRight, e));
        } else main.DimAll(null);
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
        stands.TryGetValue(profile, out var x) ? x.e : (Emote?) null;

    public static StandLocation? UpdateStandEmote(IDialogueProfile profile, Emote emote) {
        if (!stands.ContainsKey(profile)) return null;
        var location = stands[profile].loc;
        stands[profile] = (location, emote);
        var (flip, sprites) = IsLeft(location) ? profile.FindLeftStand(emote) : profile.FindRightStand(emote);
        var target = main.Target(location);
        main.NewPiecewise(location, sprites);
        var tr = target.transform;
        var ls = tr.localScale;
        ls.x = Mathf.Abs(ls.x) * (flip ? -1 : 1);
        tr.localScale = ls;
        return location;
    }

    private static IEnumerator _FadeStand(PiecewiseRender pr, float time, bool fadeIn, ICancellee cT, Action done) {
        var (t0, d) = fadeIn ? (0, ETime.FRAME_TIME) : (time - ETime.FRAME_TIME, -ETime.FRAME_TIME);
        for (; 0 <= t0 && t0 < time; t0 += d) {
            pr.ApplyColor(pr.lastColor.WithA(t0 / time));
            if (cT.Cancelled) {
                done();
                yield break;
            }
            yield return null;
        }
        pr.ApplyColor(pr.lastColor.WithA(fadeIn ? 1 : 0));
        done();
    }

    public static void FadeStand(IDialogueProfile profile, float time, bool fadeIn, ICancellee cT, Action done) {
        if (!main.renders.TryGetValue(stands[profile].loc, out var pr))
            throw new Exception($"Cannot fade stand that is not being rendered: {profile.DisplayName}");
        main.RunRIEnumerator(_FadeStand(pr, time, fadeIn, new JointCancellee(cT, pr.cT), done));
    }

    public static void SetOpacity(IDialogueProfile profile, float opacity) {
        var r = main.renders[stands[profile].loc];
        r.ApplyColor(r.lastColor.WithA(opacity));
    }

    private static readonly Dictionary<IDialogueProfile, (StandLocation loc, Emote e)> stands =
        new Dictionary<IDialogueProfile, (StandLocation, Emote)>();

    public static void SetStand(IDialogueProfile profile, StandLocation? loc, Emote? emote) {
        var location = loc ?? (stands.TryGetValue(profile, out var le) ?
            le.loc :
            throw new Exception($"No existing location for profile {profile.DisplayName}"));
        var emt = emote ?? (stands.TryGetValue(profile, out le) ?
            le.e :
            throw new Exception($"No existing emote for profile {profile.DisplayName}"));
        stands[profile] = (location, emt);
        loc = UpdateStandEmote(profile, emt);
        if (currSpeaker == profile) main.DimAll(loc);
    }

    private static readonly Color activeSpeaker = Color.white;
    private static readonly Color inactiveSpeaker = new Color(0.4f, 0.4f, 0.4f, 1f);

    public static void RunDialogue(TextCommand<DialogueObject>[] words, ICancellee cT, Action done, bool continued = false) {
        main.RunRIEnumerator(main._RunDialogue(words, cT, done, continued));
    }

    public static void HideDialogue() {
        WaitingOnConfirm = false;
        //This may get called on scene destroy
        if (main != null) {
            foreach (var pw in main.renders.Values.ToArray()) pw.DestroySpawned();
            main.renders.Clear();
            if (main.gameObject.activeInHierarchy) main.total.SetActive(false);
        }
        DialogueActive = false;
    }

    public static void ShowAndResetDialogue() {
        WaitingOnConfirm = false;
        foreach (var pw in main.renders.Values.ToArray()) pw.DestroySpawned();
        main.renders.Clear();
        main.leftIcon.gameObject.SetActive(false);
        main.rightIcon.gameObject.SetActive(false);
        currLeft = currRight = null;
        main.leftSpeaker.text = "";
        main.rightSpeaker.text = "";
        main.left1.color = main.left2.color = main.right1.color = main.right2.color = main.center.color = Color.white;
        stands.Clear();
        main.total.SetActive(true);
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
}