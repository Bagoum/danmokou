using DMK.Core;
using DMK.Player;

namespace DMK.Achievements {

public class TutorialCompletedReq : Requirement {
    public TutorialCompletedReq() {
        Listen(SaveData.Record.TutorialCompleted);
    }
    public override State EvalState() => SaveData.r.TutorialDone.ToACVState();
}


}