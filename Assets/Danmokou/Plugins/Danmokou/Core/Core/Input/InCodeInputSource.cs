using System.Collections.Generic;

namespace Danmokou.Core.DInput {
public class InCodeInputSource : IInputHandlerInputSource, IInputSource {
    public List<IInputHandler> Handlers { get; } = new();
    private readonly Queue<(MockInputChecker, bool)> delayedEnables = new();
    public InCodeInputSource() {
        mDialogueConfirm = new(this);
        mDialogueSkipAll = new(this);
        dialogueConfirm = InputHandler.Trigger(mDialogueConfirm);
        dialogueSkipAll = InputHandler.Trigger(mDialogueSkipAll);
        Handlers.AddRange(new[] {
            dialogueConfirm, dialogueSkipAll
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

    public MockInputChecker mDialogueConfirm { get; }
    public MockInputChecker mDialogueSkipAll { get; }
    private IInputHandler dialogueConfirm { get; }
    private IInputHandler? dialogueSkipAll { get; }
    public bool? DialogueConfirm => dialogueConfirm.Active ? true : null;
    public bool? DialogueSkipAll => (dialogueSkipAll?.Active is true) ? true : null;
}
}