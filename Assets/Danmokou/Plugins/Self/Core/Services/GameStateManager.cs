using System;
using Danmaku;
using JetBrains.Annotations;
using UnityEngine;

public enum GameState {
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

public static class GSHelpers {
    public static bool IsPaused(this GameState gs) => gs == GameState.PAUSE || 
                                                      gs == GameState.DEATH || 
                                                      gs == GameState.SUCCESS ||
                                                      gs == GameState.EFFECTPAUSE ||
                                                      gs == GameState.NOINPUTPAUSE;

    public static bool InputAllowed(this GameState gs) => gs != GameState.NOINPUTPAUSE && gs != GameState.LOADING;
}
public static class GameStateManager {
    public static bool InputAllowed => state.InputAllowed();
    private static GameState state = GameState.RUN;
    [CanBeNull] private static Action stateUpdate;

    [CanBeNull] public static Action<Action> UnpauseAnimator { get; set; }

    public static void CheckForStateUpdates() {
        if (PauseAllowed && InputManager.Pause.Active && stateUpdate == null) {
            if (state == GameState.RUN) {
                stateUpdate = _Pause;
            } else if (state == GameState.PAUSE) {
                UIUnpause();
            }
        }
    }

    public static void UIUnpause() {
        stateUpdate = _NoInputPause;
        if (UnpauseAnimator == null) stateUpdate = _Unpause;
        else UnpauseAnimator(() => stateUpdate = _Unpause);
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
    public static bool IsLoading => state == GameState.LOADING;
    public static bool IsPaused => state == GameState.PAUSE;
    public static bool IsRunning => state == GameState.RUN;
    public static bool IsLoadingOrPaused => IsLoading || state.IsPaused();
    public static bool IsDeath => state == GameState.DEATH;

    private static void __SetAndRaise(GameState s) {
        Log.Unity($"Setting game state to {s}");
        state = s;
        Core.Events.GameStateHasChanged.Publish(s);
    }

    private static void _PauseType(GameState gs) {
        Time.timeScale = 0f;
        __SetAndRaise(gs);
    }

    private static void _EffectPause() => _PauseType(GameState.EFFECTPAUSE);
    private static void _Sucess() => _PauseType(GameState.SUCCESS);
    private static void _Death() => _PauseType(GameState.DEATH);
    private static void _Pause() => _PauseType(GameState.PAUSE);
    private static void _NoInputPause() => _PauseType(GameState.NOINPUTPAUSE);
    private static void _Unpause() {
        Time.timeScale = 1f;
        __SetAndRaise(GameState.RUN);
    }
    private static void _SetLoading(bool on) {
        if (on) _PauseType(GameState.LOADING);
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
        if (stateUpdate == null && state == GameState.RUN) {
            stateUpdate = _EffectPause;
            toNormal = () => {
                if (state == GameState.EFFECTPAUSE) stateUpdate = _Unpause;
            };
            return true;
        }
        toNormal = null;
        return false;
    }
}
