﻿using Danmokou.Core;
using UnityEngine;

namespace Danmokou.UI {
public class DifficultyCommentator : Commentator<FixedDifficulty?> {
    public Comment customComment;
    public Comment easyComment;
    public Comment normalComment;
    public Comment hardComment;
    public Comment lunaticComment;

    public override void SetCommentFromValue(FixedDifficulty? fd) => SetComment(fd switch {
        FixedDifficulty.Easy => easyComment,
        FixedDifficulty.Normal => normalComment,
        FixedDifficulty.Hard => hardComment,
        FixedDifficulty.Lunatic => lunaticComment,
        _ => customComment
    });

}
}