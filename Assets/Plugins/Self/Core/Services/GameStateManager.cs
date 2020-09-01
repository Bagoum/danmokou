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
    //Same basic structure as DEATH, but without killing the player.
    SUCCESS = 5,
}

public static class GSHelpers {
    public static bool IsPaused(this GameState gs) => gs == GameState.PAUSE || 
                                                      gs == GameState.DEATH || 
                                                      gs == GameState.SUCCESS;
}
public static class GameStateManager {
    private static GameState state = GameState.RUN;
    [CanBeNull] private static Action stateUpdate;
    private static float prePauseTimeScale = 1f;


    public static void CheckForStateUpdates() {
        if (PauseAllowed && InputManager.Pause.Active) {
            if (state == GameState.RUN) {
                stateUpdate = _Pause;
            } else if (state == GameState.PAUSE) {
                stateUpdate = _Unpause;
            }
        }
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
        Core.Events.GameStateHasChanged.Invoke(s);
    }

    private static void _PauseType(GameState gs) {
        prePauseTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        __SetAndRaise(gs);
    }

    private static void _Sucess() => _PauseType(GameState.SUCCESS);
    private static void _Death() => _PauseType(GameState.DEATH);
    private static void _Pause() => _PauseType(GameState.PAUSE);
    private static void _Unpause() {
        Time.timeScale = prePauseTimeScale;
        __SetAndRaise(GameState.RUN);
    }
    private static void _SetLoading(bool on) {
        __SetAndRaise(on ? GameState.LOADING : GameState.RUN);
    }

    public static void ForceUnpause() => stateUpdate = _Unpause;

    public static void SetLoading(bool on) => stateUpdate = () => _SetLoading(on);

    private static DeletionMarker<Action<CampaignMode>> playerDeathListener = Core.Events.PlayerHasDied.Listen(HandlePlayerDeath);
    private static void HandlePlayerDeath(CampaignMode m) {
        stateUpdate = _Death;
    }

    public static void SendSuccessEvent() => stateUpdate = _Sucess;
}
