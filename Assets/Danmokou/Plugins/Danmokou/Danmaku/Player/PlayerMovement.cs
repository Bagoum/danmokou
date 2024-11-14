using System;
using BagoumLib;
using BagoumLib.Functional;
using BagoumLib.Mathematics;
using Danmokou.Behavior.Display;
using Danmokou.Core;
using Danmokou.Core.DInput;
using Danmokou.DMath;
using Danmokou.Scriptables;
using Danmokou.Services;
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

    public record Grid(Vector2 Center, Vector2 Unit, Vector2Int MinCoord, Vector2Int MaxCoord, float MovTime, SFXConfig? onMove) : PlayerMovement {
        private (Vector2Int to, float elapsed)? currMov;
        private Vector2Int currCoord = Vector2Int.zero;
        private Vector2Int? inputBuffer = null;

        private Vector2 CoordToWorld(Vector2Int coord) => Center + Unit.PtMul(coord);

        private bool IsInBounds(Vector2Int coord) =>
            coord.x >= MinCoord.x && coord.x <= MaxCoord.x && coord.y >= MinCoord.y && coord.y <= MaxCoord.y;

        private Vector2Int GetMov(PlayerController p) {
            if (!p.AllowPlayerInput) goto end;
            if (InputManager.GetKeyTrigger(KeyCode.D).Active)
                return Vector2Int.right;
            if (InputManager.GetKeyTrigger(KeyCode.W).Active)
                return Vector2Int.up;
            if (InputManager.GetKeyTrigger(KeyCode.A).Active)
                return Vector2Int.left;
            if (InputManager.GetKeyTrigger(KeyCode.S).Active)
                return Vector2Int.down;
            end: ;
            return Vector2Int.zero;
        }

        public override void Setup() {
            base.Setup();
            UpdateGridDisplay(currCoord);
        }

        private void UpdateGridDisplay(Vector2Int nxt) =>
            ServiceLocator.MaybeFind<PlayerMovementGridDisplay>().ValueOrNull()?.SelectEntry(nxt);

        public override Vector2 UpdateNextDesiredDelta(PlayerController player, ShipConfig ship, float dT, out bool didInput) {
            didInput = false;
            var mov = GetMov(player);
            if (mov == Vector2Int.zero) {
                if (inputBuffer is { } buffer) {
                    mov = buffer;
                    inputBuffer = null;
                } else
                    goto update;
            } else
                didInput = true;
            var nxt = currCoord + mov;
            if (!IsInBounds(nxt)) {
                //TODO: failure sfx if no currMov
                goto update;
            }
            if (currMov is null) {
                ISFXService.SFXService.Request(onMove);
                UpdateGridDisplay(nxt);
                currMov = (nxt, 0f);
            } else {
                inputBuffer = mov;
            }
            update: ;
            if (currMov is not null) {
                var (to, elapsed) = currMov.Value;
                elapsed += dT;
                if (elapsed >= MovTime) {
                    currMov = null;
                    currCoord = to;
                    return CoordToWorld(to) - (Vector2)player.tr.position;
                }
                currMov = (to, elapsed);
                var nxtWorldLoc = Vector2.Lerp(CoordToWorld(currCoord), CoordToWorld(to),
                    Easers.EOutQuad(elapsed / MovTime));
                return nxtWorldLoc - (Vector2)player.tr.position;
            }
            return Vector2.zero;
        }
    }
}
}