using System.Collections.Generic;

namespace Danmokou.Core.DInput {
public class InCodeInputSource : IInputHandlerInputSource, IInputSource {
    public List<IInputHandler> Handlers { get; } = new();
    private readonly Queue<(MockInputChecker, bool)> delayedEnables = new();
    public InCodeInputSource() {
        mUIConfirm = new(this);
        mDialogueSkipAll = new(this);
        uiConfirm = InputHandler.Trigger(mUIConfirm);
        dialogueSkipAll = InputHandler.Trigger(mDialogueSkipAll);
        Handlers.AddRange(new[] {
            uiConfirm, dialogueSkipAll
        });
    }
    
    public void OncePerUnityFrameToggleControls() {
        var nUpdates = delayedEnables.Count;
        while (nUpdates-- > 0) {
            var (m, e) = delayedEnables.Dequeue();
            m._AddCounter(e ? 1 : -1);
            if (e)
                delayedEnables.Enqueue((m, false));
        }
        ((IInputHandlerInputSource)this).UpdateHandlers();
    }

    public void SetActive(MockInputChecker m) => delayedEnables.Enqueue((m, true));

    public MockInputChecker mUIConfirm { get; }
    public MockInputChecker mDialogueSkipAll { get; }
    private IInputHandler uiConfirm { get; }
    private IInputHandler? dialogueSkipAll { get; }

    public bool? UIConfirm => uiConfirm.Active ? true : null;
    public bool? DialogueConfirm => UIConfirm;
    public bool? DialogueSkipAll => (dialogueSkipAll?.Active is true) ? true : null;
}
}