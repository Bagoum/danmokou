using System;
using System.Collections.Generic;
using System.Linq;
using Danmaku;
using Danmaku.DanmakuUI;
using UnityEngine.UIElements;
using static GameManagement;

public static class XMLUtils {
    public const string monospaceClass = "monospace";
    public const string largeClass = "large";
    public const string smallClass = "small";
    public const string small3Class = "small3";
    public const string shotDescrClass = "descriptor";
    public const string visibleAdjacentClass = "visibleadjacent";
    
    public static UIScreen ReplayScreen(bool showScore, Action<List<int>> cacheTentative, Action cacheConfirm) => 
        new UIScreen(SaveData.p.ReplayData.Count.Range().Select(i => 
            new CacheNavigateUINode(cacheTentative, () => 
                    SaveData.p.ReplayData.TryN(i)?.metadata.AsDisplay(showScore) ?? "---Deleted Replay---",
                new FuncNode(() => {
                    cacheConfirm();
                    return GameRequest.ViewReplay(SaveData.p.ReplayData.TryN(i));
                }, "View"),
                new ConfirmFuncNode(() => SaveData.p.TryDeleteReplay(i), "Delete", true)
            ).With(monospaceClass).With(small3Class)
        ).ToArray());
}