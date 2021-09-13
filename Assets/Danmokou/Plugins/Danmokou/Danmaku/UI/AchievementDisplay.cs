using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib.Mathematics;
using Danmokou.Achievements;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Reflection;
using Danmokou.Scriptables;
using Danmokou.Services;
using TMPro;
using UnityEngine;

namespace Danmokou.UI {

/// <summary>
/// This object is created once per scene and handles all achievement display sequentially.
/// </summary>
public class AchievementDisplay : CoroutineRegularUpdater {
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
    [ReflectInto(typeof(Easer))]
    public string inLerp = "ceoutback(2.2, t)";
    private Easer InLerp = null!;
    public float waitTime;
    public Vector2 endOffset = new Vector2(0, 3);
    public float outTime;
    [ReflectInto(typeof(Easer))]
    public string outLerp = "einsine(t)";
    private Easer OutLerp = null!;
    public SFXConfig? sfx;

    private void Awake() {
        tr = transform;
        baseLoc = (Vector2)tr.localPosition - 
                  (uiParent.autoShiftCamera ? GameManagement.References.bounds.center : Vector2.zero);
        InLerp = inLerp.Into<Easer>();
        OutLerp = outLerp.Into<Easer>();
        Hide();
    }

    private void Hide() => displayContainer.SetActive(false);
    private void Show() => displayContainer.SetActive(true);

    protected override void BindListeners() {
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
        ServiceLocator.SFXService.Request(sfx);
        var task = tr.GoTo(baseLoc, inTime, InLerp).Run(this);
        while (!task.IsCompleted)
            yield return null;
        for (float t = 0; t < waitTime; t += ETime.FRAME_TIME)
            yield return null;
        task = tr.GoTo(baseLoc + endOffset, outTime, OutLerp).Run(this);
        while (!task.IsCompleted)
            yield return null;
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