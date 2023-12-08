using System;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Scriptables;
using UnityEngine;

namespace Danmokou.Player {
public abstract partial record PlayerMovement {
    public abstract Vector2 UpdateNextDesiredDelta(PlayerController player, ShipConfig ship, float dT);
    public virtual void UpdateMovementNotAllowed(PlayerController player, ShipConfig ship, float dT) { }

    /// <summary>
    /// The player can control their movement in LRUD axes without any momentum.
    /// </summary>
    public record Standard : PlayerMovement {
        
        public override Vector2 UpdateNextDesiredDelta(PlayerController player, ShipConfig ship, float dT) {
            return player.DesiredMovement01 * (player.CombinedSpeedMultiplier * dT);
        }
    }

    public record Flappy : PlayerMovement {
        public float JumpCooldown { get; init; } = 0.5f;
        public float JumpVel { get; init; } = 3f;
        public float Gravity { get; init; } = 4.6f;
        public float MaxInheritedHorizontalSpeed { get; init; } = 4f;
        public float MaxFallSpeed { get; init; } = 5f;
        private float timeToNextJump = 0f;
        
        public override Vector2 UpdateNextDesiredDelta(PlayerController player, ShipConfig ship, float dT) {
            var dst = player.DesiredMovement01 * (player.CombinedSpeedMultiplier * dT);
            var x = M.Lerp(
                M.Clamp(-MaxInheritedHorizontalSpeed * dT, MaxInheritedHorizontalSpeed * dT, player.LastDelta.x)
                , dst.x, 1f * ETime.FRAME_TIME);
            float y;
            if ((timeToNextJump -= dT) <= 0f && dst.y > 0) {
                timeToNextJump = JumpCooldown;
                y = JumpVel * dT;
                player.SpawnedShip.displayer!.Animate(AnimationType.Attack, false, null);
            } else
                y = player.LastDelta.y - Gravity * dT * dT;
            return new Vector2(x, Math.Max(-MaxFallSpeed * dT, y));
        }

        public override void UpdateMovementNotAllowed(PlayerController player, ShipConfig ship, float dT) {
            timeToNextJump -= dT;
        }
    }
}
}