using System;
using BagoumLib.Events;
using Danmokou.Behavior;
using UnityEngine;

[Serializable]
public struct CursorData {
    public Texture2D? cursor;
    public Vector2 offset;
}

public enum CursorMode {
    Default,
    Button,
    Text,
}

public class CursorManager : MonoBehaviour {
    public static OverrideEvented<CursorMode> Mode { get; } = new(CursorMode.Default);
    public static IDisposable AddButton() => Mode.AddConst(CursorMode.Button);
    
    public CursorData dflt;
    public CursorData button;
    public CursorData text;

    private void Awake() {
        SetCursorForMode(CursorMode.Default);
    }

    private void Update() {
        SetCursorForMode(Mode.Value);
    }

    private CursorMode? lastMode;
    private void SetCursorForMode(CursorMode mode) {
        if (mode == lastMode) return;
        lastMode = mode;
        var data = mode switch {
            CursorMode.Button => button,
            CursorMode.Text => text,
            _ => dflt
        };
        Cursor.SetCursor(data.cursor, data.offset, UnityEngine.CursorMode.Auto);
    }
}