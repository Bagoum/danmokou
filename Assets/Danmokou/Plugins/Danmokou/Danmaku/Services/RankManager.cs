using BagoumLib.Events;
using Danmokou.Behavior;
using Danmokou.Behavior.Items;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.GameInstance;
using Danmokou.Player;
using UnityEngine;
using static Danmokou.Services.GameManagement;

namespace Danmokou.Services {
public class RankManager : RegularUpdater {
    /// <summary>
    /// True iff rank level was increased
    /// </summary>
    public static readonly IBSubject<bool> RankLevelChanged = new Event<bool>();
    #region Consts

    /// <summary>
    /// Inclusive
    /// </summary>
    public const int minRankLevel = 0;
    /// <summary>
    /// Inclusive
    /// </summary>
    public const int maxRankLevel = 42;

    //Note: since you get half of this by default, you only need the other half to go up or down a level.

    public static double RankPointsRequiredForLevel(int level) =>
        2000;

    ///M.BlockRound(100, 1000 * (1 + Math.Log(Math.Max(1, level), 4)));
    
    public static double DefaultRankPointsForLevel(int level) => RankPointsRequiredForLevel(level) * 0.5;

    public const double RankPointsGraze = 8;
    public const double RankPointsCollectItem = 3;
    public const double RankPointsMissedItem = -5;
    public const double RankPointsScoreExtend = 420;
    public const double RankPointsDeath = -10000;
    public const double RankPointsBomb = -1000;

    public static double RankPointsForCard(CardRecord cr) => 100 * cr.stars;
    public static double RankPointsPerSecond => M.Lerp(0, 3, Difficulty.Counter, 10, 42);

    #endregion

    protected override void BindListeners() {
        base.BindListeners();
        Listen(Item.ItemCollected, _ => Instance.AddRankPoints(RankPointsCollectItem));
        Listen(Item.ItemCulled, _ => Instance.AddRankPoints(RankPointsMissedItem));
        Listen(PlayerBombs.BombFired, _ => Instance.AddRankPoints(RankPointsBomb));
    }

    public override void RegularUpdate() {
    }
    
}
}