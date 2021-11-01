using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Culture;
using BagoumLib.Tasks;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Scriptables;
using Danmokou.Services;
using Danmokou.SM;
using Danmokou.VN;
using Suzunoya.ControlFlow;
using SuzunoyaUnity;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {
/// <summary>
/// Class to display the dialogue log of an executing VN.
/// </summary>
[Preserve]
public class XMLVNBacklogMenu : PausedGameplayMenu, IVNBacklog {
    public static int BacklogCount { get; set; } = 69;
    private ICancellee? currToken;
    private ExecutingVN? currVn;
    private ExecutingVN? CurrVN {
        get => currToken?.Cancelled == true ? (currVn = null) : currVn;
        set => currVn = value;
    }

    public XMLPauseMenu pauseMenu = null!;
    public VisualTreeAsset BacklogEntry = null!;

    private ScrollView logScroll = null!;
    
    public override void FirstFrame() {
        MainScreen = new UIScreen(this, new UINode("temp")).With(UIScreen);
        
        base.FirstFrame();
        UI.Q<Label>("Header").text = "Backlog";
        logScroll = UI.Q<ScrollView>();
        HideMe();
        MenuActive = false;
    }

    protected override void BindListeners() {
        RegisterService<IVNBacklog>(this);
    }


    protected override void ResetCurrentNode() {
        Current = MainScreen.Top[MainScreen.Top.Length - 1];
    }

    //TODO Workaround for https://issuetracker.unity3d.com/issues/nullreferenceexception-gets-thrown-in-the-console-when-the-label-text-value-contains-cjk-characters-and-text-wrap-is-enabled
    private string FilterCJK(string s) {
        bool foundRemovable = false;
        for (int ii = 0; ii < s.Length; ++ii) {
            if (s[ii] >= 128) {
                foundRemovable = true;
                break;
            }
        }
        if (!foundRemovable)
            return s;
        //Log.Unity($"Filtering CJK characters out of {s} for UITK");
        var sb = new StringBuilder();
        for (int ii = 0; ii < s.Length; ++ii) {
            if (s[ii] < 128)
                sb.Append(s[ii]);
        }
        return sb.ToString();
    }

    private async Task BacklogTo(Action<VNLocation> backlogger, VNLocation loc) {
        var overlay = ServiceLocator.MaybeFind<IUIScreenOverlay>();
        if (overlay is null) {
            backlogger(loc);
            ProtectHide();
            return;
        }
        using var disabler = UpdatesEnabled.AddConst(false);
        using var pdisabler = pauseMenu.UpdatesEnabled.AddConst(false);
        await overlay.Fade(null, 1, 0.8f, null);
        backlogger(loc);
        HideMe();
        await overlay.Fade(1, 1, 0.5f, null);
        await overlay.Fade(null, 0, 0.4f, null);
    }

    private UINode MakeNode(DialogueLogEntry entry) {
        var node = new UINode($"<smallcaps>{entry.speakerName}</smallcaps>")
            .OnBound(ve => ve.Q<Label>("Description").text = entry.readableSpeech)
            .OnBound(ve => {
                if (entry.speakerSprite != null)
                    ve.Q("Speaker").style.backgroundImage = new StyleBackground(entry.speakerSprite);
            })
            .With((s, ve) => {
                var b = ve.Q("Borderer");
                var smul = s == NodeState.Focused ? Color.white : new Color(0.75f, 0.75f, 0.75f, 1f);
                b.style.borderTopColor = entry.uiColor * smul;
                b.style.borderLeftColor = entry.uiColor * smul * new Color(0.65f, 0.65f, 0.65f);
            })
            .With((s, ve) => ve.Q<Label>("Description").style.color = ve.Q<Label>("Label").style.color = 
                entry.textColor * (s == NodeState.Focused ? Color.white : new Color(0.75f, 0.75f, 0.75f, 1f)))
            .With(BacklogEntry);
        var backlog = CurrVN?.doBacklog;
        if (backlog != null && !(entry.location is null))
            node.SetConfirmOverride(() => {
                _ = BacklogTo(backlog, entry.location).ContinueWithSync(() => { });
                return (true, node);
            });
        return node;
    }

    private void ReconstructScreen() {
        if (CurrVN == null) return;
        var nodes = new List<UINode>();
        for (int ii = Math.Max(0, CurrVN.backlog.Published.Count - BacklogCount); ii < CurrVN.backlog.Published.Count; ++ii)
            nodes.Add(MakeNode(CurrVN.backlog.Published[ii]));
        MainScreen.AssignNewNodes(nodes.ToArray());
    }

    private bool openQueued = false;
    public override void RegularUpdate() {
        if (RegularUpdateGuard) {
            if (MenuActive && (InputManager.VNBacklogPause.Active || InputManager.Pause.Active || 
                               (Input.mouseScrollDelta.y < 0 && logScroll.verticalScroller.value >= logScroll.verticalScroller.highValue))) {
                ProtectHide();
            } else if (!MenuActive && (InputManager.VNBacklogPause.Active || openQueued || Input.mouseScrollDelta.y > 0) &&
                       EngineStateManager.State == EngineState.RUN && CurrVN?.backlog.Published.Count > 0) {
                ReconstructScreen();
                ShowMe();
            }
            openQueued = false;
        }
        base.RegularUpdate();
    }

    public Cancellable TryRegister(ExecutingVN evn) {
        if (CurrVN != null)
            throw new Exception("Failed to register Executing VN");//return null;
        CurrVN = evn;
        var ret = new Cancellable();
        currToken = ret;
        return ret;
    }

    public void Open() => openQueued = true;
}
}