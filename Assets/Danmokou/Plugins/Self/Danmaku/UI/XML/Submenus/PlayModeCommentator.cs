using DMK.Core;
using UnityEngine;

namespace DMK.UI {
public class PlayModeCommentator : Commentator {
    public enum Mode {
        MAIN,
        EX,
        STAGEPRAC,
        BOSSPRAC,
        TUTORIAL
    }

    public Comment mainComment;
    public Comment exComment;
    public Comment stPracComment;
    public Comment bossPracComment;
    public Comment tutorialComment;

    public Comment mainNoTutorialComment;
    public Comment lockedComment;


    public void SetComment(Mode m, bool locked) => SetComment(
        locked ? lockedComment :
        (!SaveData.r.TutorialDone && m != Mode.TUTORIAL) ? mainNoTutorialComment :
        m switch {
            Mode.EX => exComment,
            Mode.STAGEPRAC => stPracComment,
            Mode.BOSSPRAC => bossPracComment,
            Mode.TUTORIAL => tutorialComment,
            _ => mainComment
    });
}
}