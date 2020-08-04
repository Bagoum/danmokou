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
}

public static class GSHelpers {
    public static bool IsPaused(this GameState gs) => gs == GameState.PAUSE || gs == GameState.DEATH;
}
public class GameStateManager : MonoBehaviour {
    private static GameStateManager main;
    private static GameState state = GameState.RUN;
    [CanBeNull] private static Action stateUpdate;
    private static float prePauseTimeScale = 1f;

    private void Awake() {
        main = this;
    }

    private void Update() {
        if (!IsLoading) {
            if (PauseAllowed && InputManager.Pause.Active) {
                if (state == GameState.RUN) {
                    stateUpdate = _Pause;
                } else if (state == GameState.PAUSE) {
                    stateUpdate = _Unpause;
                }
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
    private static void _Death() {
        prePauseTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        __SetAndRaise(GameState.DEATH);
    }
    private static void _Pause() {
        prePauseTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        __SetAndRaise(GameState.PAUSE);
    }
    private static void _Unpause() {
        Time.timeScale = prePauseTimeScale;
        __SetAndRaise(GameState.RUN);
    }
    private static void _SetLoading(bool on) {
        __SetAndRaise(on ? GameState.LOADING : GameState.RUN);
    }

    public static void ForceUnpause() => stateUpdate = _Unpause;

    public static void SetLoading(bool on) => stateUpdate = () => _SetLoading(on);

    private DeletionMarker<Action<CampaignMode>> playerDeathListener;
    public void OnEnable() {
        playerDeathListener = Core.Events.PlayerHasDied.Listen(HandlePlayerDeath);
    }
    private void OnDisable() {
        playerDeathListener.MarkForDeletion();
    }
    private static void HandlePlayerDeath(CampaignMode m) {
        stateUpdate = _Death;
    }
}
