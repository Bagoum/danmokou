using Danmokou.Core;
using Danmokou.Services;
using UnityEngine;

namespace Danmokou.UI {
public record PlayModeStatus(PlayModeCommentator.Mode Mode, bool Locked) {
    public bool TutorialIncomplete { get; init; } = false;
}
public class PlayModeCommentator : Commentator<PlayModeStatus> {
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


    public override void SetCommentFromValue(PlayModeStatus v) => SetComment(
        v.Locked ? lockedComment :
        (v.TutorialIncomplete && v.Mode != Mode.TUTORIAL) ? 
            mainNoTutorialComment :
            v.Mode switch {
                Mode.EX => exComment,
                Mode.STAGEPRAC => stPracComment,
                Mode.BOSSPRAC => bossPracComment,
                Mode.TUTORIAL => tutorialComment,
                _ => mainComment
    });
}
}