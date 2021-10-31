using System.Reactive;
using BagoumLib.Events;
using Danmokou.Behavior.Display;
using Danmokou.DMath;
using UnityEngine;
using static Danmokou.DMath.LocationHelpers;

namespace Danmokou.Player {
public partial class PlayerController {
    public enum PlayerState {
        NORMAL,
        WITCHTIME,
        RESPAWN,
        NULL
    }

    public enum DeathbombState {
        NULL,
        WAITING,
        PERFORMED
    }

    #region Consts
    private const float RespawnFreezeTime = 0.1f;
    private const float RespawnDisappearTime = 0.5f;
    private const float RespawnMoveTime = 1.5f;
    private static Vector2 RespawnStartLoc => new Vector2(0, Bot - 1f);
    private static Vector2 RespawnEndLoc => new Vector2(0, BotPlayerBound + 1f);
    private const float WitchTimeSpeedMultiplier = 1.4f;//2f;
    private const float WitchTimeSlowdown = 0.5f;//0.25f;
    private const float WitchTimeAudioMultiplier = 0.8f;
    
    private const float FreeFocusLerpTime = 0.3f;
    
    private static readonly IGradient scoreGrad = DropLabel.MakeGradient(
        new Color32(100, 150, 255, 255), new Color32(80, 110, 255, 255));
    private static readonly IGradient scoreGrad_bonus = DropLabel.MakeGradient(
        new Color32(20, 220, 255, 255), new Color32(10, 170, 255, 255));
    private static readonly IGradient pivGrad = DropLabel.MakeGradient(
        new Color32(0, 235, 162, 255), new Color32(0, 172, 70, 255));
    private const int ITEM_LABEL_BUFFER = 4;
    
    private static bool StateAllowsInput(PlayerState s) =>
        s switch {
            PlayerState.RESPAWN => false,
            _ => true
        };

    private static bool StateAllowsLocationUpdate(PlayerState s) =>
        s switch {
            PlayerState.RESPAWN => false,
            _ => true
        };

    private static float StateSpeedMultiplier(PlayerState s) {
        return s switch {
            PlayerState.WITCHTIME => WitchTimeSpeedMultiplier,
            _ => 1f
        };
    }

    #endregion
    
    
    #region Events
    /// <summary>
    /// Called when the player activates the meter.
    /// </summary>
    public static readonly Event<Unit> PlayerActivatedMeter = new Event<Unit>();
    /// <summary>
    /// Called when the player deactivates the meter.
    /// </summary>
    public static readonly Event<Unit> PlayerDeactivatedMeter = new Event<Unit>();
    /// <summary>
    /// Called every frame during meter activation.
    /// </summary>
    public static readonly IBSubject<Color> MeterIsActive = new Event<Color>();
    
    #endregion
}
}