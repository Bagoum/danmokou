using System.Collections.Generic;
using System.Threading.Tasks;
using Danmokou.Core;
using Danmokou.Dialogue;
using Danmokou.Services;

namespace Danmokou.SM {
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
    private readonly TTaskPattern func;
    public ReflectableSLSM(TTaskPattern func) {
        this.func = func;
    }
    public override Task Start(SMHandoff smh) => func(smh);
}


/// <summary>
/// `endcard`: Controls for endcard display in dialogue scripts. 
/// </summary>
[Reflect]
public class EndcardControllerTSM : ReflectableSLSM {
    public delegate Task Endcard(SMHandoff smh);
    
    public EndcardControllerTSM(Endcard rs) : base(new TTaskPattern(rs)) {}

    /// <summary>
    /// Turn the endcard controller on. It will appear black.
    /// </summary>
    public static Endcard Activate() => smh => {
        Endcards.Activate();
        return Task.CompletedTask;
    };
    
    /// <summary>
    /// Fade in an endcard image.
    /// </summary>
    public static Endcard FadeIn(float t, string key) => smh => {
        Endcards.FadeIn(t, key, smh.cT, WaitingUtils.GetAwaiter(out Task task));
        return task;
    };
    /// <summary>
    /// Fade out an endcard image (to black).
    /// </summary>
    public static Endcard FadeOut(float t) => smh => {
        Endcards.FadeOut(t, smh.cT, WaitingUtils.GetAwaiter(out Task task));
        return task;
    };
    
}

}
