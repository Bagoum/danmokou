using System.Reactive;
using BagoumLib.Events;
using Danmokou.Behavior.Display;
using Danmokou.DMath;
using UnityEngine;
using static Danmokou.DMath.LocationHelpers;

namespace Danmokou.Player {
public partial class PlayerController {
    public enum BombContext {
        NORMAL,
        DEATHBOMB
    }
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
    private const float WitchTimeSpeedMultiplier = 1.4f;//2f;
    private const float WitchTimeSlowdown = 0.5f;//0.25f;
    private const float WitchTimeAudioMultiplier = 0.8f;
    
    private const float FreeFocusLerpTime = 0.2f;
    
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

    private static bool StateAllowsPlayerMovement(PlayerState s) =>
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
    
    
    protected static Direction CheckOOB(ref Vector2 pos, ref Vector2 velocity) {
        Direction oob = 0;
        if (pos.x < LeftPlayerBound) {
            pos.x = LeftPlayerBound;
            velocity.x = Mathf.Max(velocity.x, 0f);
            oob |= LocationHelpers.Direction.Left;
        } else if (pos.x > RightPlayerBound) {
            pos.x = RightPlayerBound;
            velocity.x = Mathf.Min(velocity.x, 0f);
            oob |= LocationHelpers.Direction.Right;
        }
        if (pos.y < BotPlayerBound) {
            pos.y = BotPlayerBound;
            velocity.y = Mathf.Max(velocity.y, 0f);
            oob |= LocationHelpers.Direction.Down;
        } else if (pos.y > TopPlayerBound) {
            pos.y = TopPlayerBound;
            velocity.y = Mathf.Min(velocity.y, 0f);
            oob |= LocationHelpers.Direction.Up;
        }
        return oob;
    }
    
    
    #region Events
    /// <summary>
    /// Called when the player activates the meter.
    /// </summary>
    public static readonly Event<Unit> PlayerActivatedMeter = new();
    /// <summary>
    /// Called when the player deactivates the meter.
    /// </summary>
    public static readonly Event<Unit> PlayerDeactivatedMeter = new();
    /// <summary>
    /// Called when the player tried to use meter, but failed.
    /// </summary>
    public static readonly Event<Unit> PlayerMeterFailed = new();
    /// <summary>
    /// Called every frame during meter activation.
    /// </summary>
    public static readonly IBSubject<Color> MeterIsActive = new Event<Color>();
    /// <summary>
    /// Called when a bomb is used.
    /// </summary>
    public static readonly IBSubject<(Ability.Bomb type, BombContext ctx)> BombFired =
        new Event<(Ability.Bomb, BombContext)>();
    
    #endregion
}
}