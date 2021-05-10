using System;
using System.Collections;
using System.Collections.Generic;
using DMK.Achievements;
using DMK.Behavior;
using DMK.Core;
using DMK.DMath;
using DMK.Reflection;
using DMK.Scriptables;
using DMK.Services;
using TMPro;
using UnityEngine;

namespace DMK.UI {

/// <summary>
/// This object is created once per scene and handles all achievement display sequentially.
/// </summary>
public class AchievementDisplay : CoroutineRegularUpdater, IAchievementDisplay {
    private class DisplayRequest {
        public readonly Achievement achievement;
        public float delayTime;
        public DisplayRequest(Achievement achievement) {
            this.achievement = achievement;
            this.delayTime = achievement.DelayTime;
        }
    }
    
    public TextMeshPro lowerText = null!;
    public GameObject displayContainer = null!;
    public UIManager uiParent = null!;
    
    private Transform tr = null!;
    private static readonly Queue<DisplayRequest> queued = new Queue<DisplayRequest>();
    private bool isRunning = false;
    private Vector2 baseLoc;
    public Vector2 startOffset = new Vector2(4, 0);
    public float inTime;
    [ReflectInto(typeof(FXY))]
    public string inLerp = "eoutback(2.2, t)";
    private FXY InLerp = null!;
    public float waitTime;
    public Vector2 endOffset = new Vector2(0, 3);
    public float outTime;
    [ReflectInto(typeof(FXY))]
    public string outLerp = "einsine(t)";
    private FXY OutLerp = null!;
    public SFXConfig? sfx;

    private void Awake() {
        tr = transform;
        baseLoc = (Vector2)tr.localPosition - 
                  (uiParent.autoShiftCamera ? GameManagement.References.bounds.center : Vector2.zero);
        InLerp = inLerp.Into<FXY>();
        OutLerp = outLerp.Into<FXY>();
        Hide();
    }

    private void Hide() => displayContainer.SetActive(false);
    private void Show() => displayContainer.SetActive(true);

    protected override void BindListeners() {
        RegisterDI<IAchievementDisplay>(this);
        Listen(Achievement.AchievementStateUpdated, a => {
            if (a.State == State.Completed) QueueDisplayForAchievement(a);
        });
        base.BindListeners();
    }

    public override void RegularUpdate() {
        if (!isRunning && queued.Count > 0) {
            if ((queued.Peek().delayTime -= ETime.FRAME_TIME) <= 0f) {
                isRunning = true;
                RunDroppableRIEnumerator(Run(queued.Dequeue().achievement));
            }
        }
        base.RegularUpdate();
    }

    private IEnumerator Run(Achievement acv) {
        lowerText.text = acv.Title;
        tr.localPosition = baseLoc + startOffset;
        Show();
        DependencyInjection.SFXService.Request(sfx);
        yield return tr.GoTo(baseLoc, inTime, InLerp);
        for (float t = 0; t < waitTime; t += ETime.FRAME_TIME)
            yield return null;
        yield return tr.GoTo(baseLoc + endOffset, outTime, OutLerp);
        Hide();
        isRunning = false;
    }

    public void QueueDisplayForAchievement(Achievement acv) => queued.Enqueue(new DisplayRequest(acv));

    [ContextMenu("test achievement")]
    public void testAcv() =>
        QueueDisplayForAchievement(new Achievement("hello", "This is the Achievement Title", "",
            () => new CompletedFixedReq(), null!));
}
}