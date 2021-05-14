using Danmokou.Core;
using Danmokou.Player;

namespace Danmokou.Achievements {

public class TutorialCompletedReq : Requirement {
    public TutorialCompletedReq() {
        Listen(SaveData.Record.TutorialCompleted);
    }
    public override State EvalState() => SaveData.r.TutorialDone.ToACVState();
}


}