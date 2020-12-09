using System;
using JetBrains.Annotations;
using UnityEngine;

namespace DMK.Core {
public enum EngineState {
    RUN = 1,
    //Pause-types: Time is frozen. Some specific objects may continue to update, but the engine itself is frozen.
    PAUSE = 2,
    DEATH = 3,
    LOADING = 4,
    ///Same basic structure as DEATH, but without killing the player.
    SUCCESS = 5,
    /// <summary>
    /// For eg. the freeze time after taking a picture with AyaCamera.
    /// </summary>
    EFFECTPAUSE = 6,
    /// <summary>
    /// During certain UI effects, such as windows sliding in/out, input is disabled to prevent weird artifacting.
    /// </summary>
    NOINPUTPAUSE = 7
}

public static class EngineStateHelpers {
    public static bool IsPaused(this EngineState gs) => gs == EngineState.PAUSE ||
                                                      gs == EngineState.DEATH ||
                                                      gs == EngineState.SUCCESS ||
                                                      gs == EngineState.EFFECTPAUSE ||
                                                      gs == EngineState.NOINPUTPAUSE;

    public static bool InputAllowed(this EngineState gs) => gs != EngineState.NOINPUTPAUSE && gs != EngineState.LOADING;
}

public interface IUnpauseAnimateProvider {
    void UnpauseAnimator(Action done);
}

public static class EngineStateManager {
    public static bool InputAllowed => state.InputAllowed();
    private static EngineState state = EngineState.RUN;
    [CanBeNull] private static Action stateUpdate;

    public static void CheckForStateUpdates() {
        if (PauseAllowed && InputManager.Pause.Active && stateUpdate == null) {
            if (state == EngineState.RUN) {
                stateUpdate = _Pause;
            } else if (state == EngineState.PAUSE) {
                AnimatedUnpause();
            }
        }
    }

    public static void AnimatedUnpause() {
        stateUpdate = _NoInputPause;
        var animator = DependencyInjection.MaybeFind<IUnpauseAnimateProvider>();
        if (animator == null) stateUpdate = _Unpause;
        else animator.UnpauseAnimator(() => stateUpdate = _Unpause);
    }

    /// <summary>
    /// Called by ETime at end of frame for consistency
    /// </summary>
    public static void UpdateGameState() {
        stateUpdate?.Invoke();
        stateUpdate = null;
    }

    public static bool PendingChange => stateUpdate != null;

    public static bool PauseAllowed { get; set; } = true;
    public static bool IsLoading => state == EngineState.LOADING;
    public static bool IsPaused => state == EngineState.PAUSE;
    public static bool IsRunning => state == EngineState.RUN;
    public static bool IsLoadingOrPaused => IsLoading || state.IsPaused();
    public static bool IsDeath => state == EngineState.DEATH;

    private static void __SetAndRaise(EngineState s) {
        Log.Unity($"Setting game state to {s}");
        state = s;
        Events.GameStateHasChanged.Publish(s);
    }

    private static void _PauseType(EngineState gs) {
        Time.timeScale = 0f;
        __SetAndRaise(gs);
    }

    private static void _EffectPause() => _PauseType(EngineState.EFFECTPAUSE);
    private static void _Sucess() => _PauseType(EngineState.SUCCESS);
    private static void _Death() => _PauseType(EngineState.DEATH);
    private static void _Pause() => _PauseType(EngineState.PAUSE);
    private static void _NoInputPause() => _PauseType(EngineState.NOINPUTPAUSE);

    private static void _Unpause() {
        Time.timeScale = 1f;
        __SetAndRaise(EngineState.RUN);
    }

    private static void _SetLoading(bool on) {
        if (on) _PauseType(EngineState.LOADING);
        else _Unpause();
    }

    public static void SetLoading(bool on, [CanBeNull] Action done) => stateUpdate = () => {
        _SetLoading(on);
        done?.Invoke();
    };

    public static void HandlePlayerDeath() {
        stateUpdate = _Death;
    }

    public static void SendSuccessEvent() => stateUpdate = _Sucess;

    public static bool TemporaryEffectPause(out Action toNormal) {
        if (stateUpdate == null && state == EngineState.RUN) {
            stateUpdate = _EffectPause;
            toNormal = () => {
                if (state == EngineState.EFFECTPAUSE) stateUpdate = _Unpause;
            };
            return true;
        }
        toNormal = null;
        return false;
    }
}
}
