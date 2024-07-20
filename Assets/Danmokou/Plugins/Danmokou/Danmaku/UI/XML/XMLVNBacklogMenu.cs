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
using Danmokou.Core.DInput;
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
    public override bool CloseOnUnscopedBack => true;
    public static int BacklogCount { get; set; } = 120;
    private ExecutingVN? currVn;
    private ExecutingVN? CurrVN {
        get => currVn is {Active: true} ? currVn :  (currVn = null);
        set => currVn = value;
    }

    public VisualTreeAsset BacklogEntry = null!;

    private ScrollView logScroll = null!;
    private UIColumn backlogEntries = null!;

    protected override Color BackgroundTint => new(0.13f, 0.05f, 0.15f);

    public override void FirstFrame() {
        MainScreen = new UIScreen(this, "BACKLOG", UIScreen.Display.OverlayTH)  { Builder = (s, ve) => {
            s.Margin.SetLRMargin(720, 720);
            ve.AddScrollColumn().style.backgroundColor = new Color(0, 0, 0, .25f);
        }, MenuBackgroundOpacity = UIScreen.DefaultMenuBGOpacity  };
        backlogEntries = new UIColumn(MainScreen, null) {
            EntryIndexOverride = () => -1
        };
        base.FirstFrame();
        logScroll = UIRoot.Q<ScrollView>();
    }

    protected override void BindListeners() {
        RegisterService<IVNBacklog>(this);
    }

    private class BacklogEntryVM : UIViewModel {
        public UINode Node { get; }
        public DialogueLogEntry Entry { get; }
        public BacklogEntryVM(UINode node, DialogueLogEntry entry) {
            Node = node;
            Entry = entry;
        }

        public override long GetViewHash() => Node.Selection.GetHashCode();
    }
    private class BacklogEntryView : UIView<BacklogEntryVM>, IUIView {
        public BacklogEntryView(BacklogEntryVM vm) : base(vm) { }

        public override void OnBuilt(UINode node) {
            base.OnBuilt(node);
            var entry = ViewModel.Entry;
            node.HTML.Q<Label>("Description").text = entry.readableSpeech;
            if (entry.speakerSprite != null)
                node.HTML.Q("Speaker").style.backgroundImage = new StyleBackground(entry.speakerSprite);
        }

        public override void UpdateHTML() {
            var entry = ViewModel.Entry;
            var vis = Node.Selection;
            var b = HTML.Q("Borderer");
            var smul = vis == UINodeSelection.Focused ? Color.white : new Color(0.75f, 0.75f, 0.75f, 1f);
            b.style.borderTopColor = entry.uiColor * smul;
            b.style.borderLeftColor = entry.uiColor * smul * new Color(0.65f, 0.65f, 0.65f);
            HTML.Q<Label>("Description").style.color =
                HTML.Q<Label>("Label").style.color =
                    entry.textColor * (vis == UINodeSelection.Focused ? Color.white : new Color(0.75f, 0.75f, 0.75f, 1f));
        }
    }
    
    private void MakeNode(DialogueLogEntry entry) {
        var backlog = CurrVN?.doBacklog;
        var node = new UINode($"<smallcaps>{entry.speakerName}</smallcaps>") {
            Prefab = BacklogEntry,
            OnConfirm = backlog != null && entry.location is not null ?
                (_, _) => {
                    backlog(entry.location);
                    return new UIResult.StayOnNode();
                } : null
        }.Bind(n => new BacklogEntryView(new(n, entry)));
        backlogEntries.AddNodeDynamic(node);
    }

    private void ReconstructScreen() {
        if (CurrVN == null) return;
        backlogEntries.ClearNodes();
        for (int ii = Math.Max(0, CurrVN.backlog.Published.Count - BacklogCount); ii < CurrVN.backlog.Published.Count; ++ii)
            MakeNode(CurrVN.backlog.Published[ii]);
    }

    private bool openQueued = false;
    public override void RegularUpdate() {
        if (RegularUpdateGuard) {
            if (IsActiveCurrentMenu && (InputManager.VNBacklogPause || InputManager.Pause ||
                               (Input.mouseScrollDelta.y < 0 && logScroll.verticalScroller.value >= logScroll.verticalScroller.highValue))) {
                CloseWithAnimationV();
            } else if (!MenuActive && (InputManager.VNBacklogPause || openQueued || Input.mouseScrollDelta.y > 0) &&
                       EngineStateManager.State == EngineState.RUN && CurrVN?.backlog.Published.Count > 0) {
                ReconstructScreen();
                OpenWithAnimationV();
            }
            openQueued = false;
        }
        base.RegularUpdate();
    }

    public void TryRegister(ExecutingVN evn) {
        if (CurrVN != null)
            throw new Exception("Failed to register Executing VN");//return null;
        CurrVN = evn;
    }

    public void QueueOpen() => openQueued = true;
}
}