using Danmokou.Core;
using Danmokou.Services;
using UnityEngine;

namespace Danmokou.UI {
public class PlayModeCommentator : Commentator<(PlayModeCommentator.Mode m, bool locked)> {
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


    public override void SetCommentFromValue((Mode m, bool locked) v) => SetComment(
        v.locked ? lockedComment :
        (!SaveData.r.TutorialDone && GameManagement.References.tutorial != null && v.m != Mode.TUTORIAL) ? 
            mainNoTutorialComment :
            v.m switch {
                Mode.EX => exComment,
                Mode.STAGEPRAC => stPracComment,
                Mode.BOSSPRAC => bossPracComment,
                Mode.TUTORIAL => tutorialComment,
                _ => mainComment
    });
}
}