using System;
using Danmokou.Behavior.Display;
using Danmokou.Core;
using Danmokou.Core.DInput;
using Danmokou.DMath;
using Danmokou.Scriptables;
using UnityEngine;

namespace Danmokou.Player {
public abstract partial record PlayerMovement {
    public virtual void Setup() { }
    public virtual void Cleanup() { }
    public abstract Vector2 UpdateNextDesiredDelta(PlayerController player, ShipConfig ship, float dT, out bool didInput);
    public virtual void UpdateMovementNotAllowed(PlayerController player, ShipConfig ship, float dT) { }

    /// <summary>
    /// The player can control their movement in LRUD axes without any momentum.
    /// </summary>
    public record Standard : PlayerMovement {
        
        public override Vector2 UpdateNextDesiredDelta(PlayerController player, ShipConfig ship, float dT, out bool didInput) {
            var moveVel = player.DesiredMovement01;
            didInput = moveVel.sqrMagnitude > 0.03f;
            return moveVel * (player.CombinedSpeedMultiplier * dT);
        }
    }

    public record Flappy : PlayerMovement {
        public float JumpCooldown { get; init; } = 0.3f;
        public float MaxHoldJumpTime { get; init; } = 0.3f;
        public float JumpVel { get; init; } = 3.5f;
        public float SlowFallGravity { get; init; } = 3f;
        public float Gravity { get; init; } = 7.5f;
        public float MaxInheritedHorizontalSpeed { get; init; } = 4f;
        public float MaxSlowFallSpeed { get; init; } = 3f;
        public float MaxFallSpeed { get; init; } = 10f;
        private float timeToNextJump = 0f;
        private float holdingJumpRemainingTime = 0f;
        public bool releasedHoldSinceLastJump = true;
        
        public override Vector2 UpdateNextDesiredDelta(PlayerController player, ShipConfig ship, float dT, out bool didInput) {
            var moveVel = player.DesiredMovement01;
            didInput = Math.Abs(moveVel.x) > 0.03f;
            var dst = moveVel * (player.CombinedSpeedMultiplier * dT);
            var x = M.Lerp(
                M.Clamp(-MaxInheritedHorizontalSpeed * dT, MaxInheritedHorizontalSpeed * dT, player.LastDelta.x)
                , dst.x, 4f * ETime.FRAME_TIME);
            float y;
            if (player.InputInControl == PlayerController.InputInControlMethod.INPUT_ACTIVE) {
                if (InputManager.IsSlowFall && player.AllowPlayerInput && player.LastDelta.y < 0) {
                    player.ShowFocusRings = true;
                    didInput = true;
                    var delta = MaxSlowFallSpeed * dT + player.LastDelta.y;
                    if (delta < 0) // player falling too fast
                        y = M.Lerp(player.LastDelta.y, -MaxSlowFallSpeed * dT, 13f * ETime.FRAME_TIME);
                    else
                        y = player.LastDelta.y - Math.Min(delta, SlowFallGravity * dT) * dT;
                } else
                    y = player.LastDelta.y - Gravity * dT * dT;
            } else {
                y = 0;
            }
                
            if (InputManager.IsFly && player.AllowPlayerInput) {
                didInput = true;
                if ((holdingJumpRemainingTime -= dT) > 0) {
                    y = JumpVel * dT;
                } else if ((timeToNextJump -= dT) <= 0f && releasedHoldSinceLastJump) {
                    timeToNextJump = JumpCooldown;
                    releasedHoldSinceLastJump = false;
                    holdingJumpRemainingTime = MaxHoldJumpTime;
                    y = JumpVel * dT;
                    player.SpawnedShip.Dependent<DisplayController>().Animate(AnimationType.Attack, false, null);
                }
            } else {
                holdingJumpRemainingTime = 0;
                timeToNextJump -= dT;
                releasedHoldSinceLastJump = true;
            }
            return new Vector2(x, Math.Max(-MaxFallSpeed * dT, y));
        }

        public override void UpdateMovementNotAllowed(PlayerController player, ShipConfig ship, float dT) {
            timeToNextJump -= dT;
        }
    }
}
}