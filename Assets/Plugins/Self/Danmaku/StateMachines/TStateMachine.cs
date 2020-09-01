using System.Collections.Generic;
using System.Threading.Tasks;
using Danmaku;

namespace SM {
/// <summary>
/// `script`: Top-level controller for dialogue files.
/// </summary>
public class ScriptTSM : SequentialSM {
    public ScriptTSM(List<StateMachine> states) : base(states) {}

    public override Task Start(SMHandoff smh) {
        Dialoguer.ShowAndResetDialogue();
        return base.Start(smh).ContinueWithSync(Dialoguer.HideDialogue);
    }
}
public abstract class ScriptLineSM : StateMachine {}
public class ReflectableSLSM : ScriptLineSM {
    private readonly TaskPattern func;
    public ReflectableSLSM(TaskPattern func) {
        this.func = func;
    }
    public override Task Start(SMHandoff smh) => func(smh);
}


/// <summary>
/// `endcard`: Controls for endcard display in dialogue scripts. 
/// </summary>
public class EndcardControllerTSM : ReflectableSLSM {
    public EndcardControllerTSM(TaskPattern rs) : base(rs) {}

    /// <summary>
    /// Turn the endcard controller on. It will appear black.
    /// </summary>
    public static TaskPattern Activate() => smh => {
        Endcards.Activate();
        return Task.CompletedTask;
    };
    
    /// <summary>
    /// Fade in an endcard image.
    /// </summary>
    public static TaskPattern FadeIn(float t, string key) => smh => {
        Endcards.FadeIn(t, key, smh.cT, WaitingUtils.GetAwaiter(out Task task));
        return task;
    };
    /// <summary>
    /// Fade out an endcard image (to black).
    /// </summary>
    public static TaskPattern FadeOut(float t) => smh => {
        Endcards.FadeOut(t, smh.cT, WaitingUtils.GetAwaiter(out Task task));
        return task;
    };
    
}

}
