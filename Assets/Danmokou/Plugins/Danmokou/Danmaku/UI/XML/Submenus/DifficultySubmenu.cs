using System;
using System.Collections.Generic;
using BagoumLib;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.UI.XML;
using UnityEngine;

namespace Danmokou.UI {
[Serializable]
public struct DifficultyDisplay {
    public FixedDifficulty dfc;
    public TelescopingDisplay display;
}
public class DifficultySubmenu : IndexedSubmenuHandler {
    public DifficultyDisplay[] difficultyDisplays = null!;
    public TelescopingDisplay? customDifficulty;
    public DifficultyCommentator? commentator;
    private List<(FixedDifficulty? key, TelescopingDisplay display)> dfcDisplays = null!;
    private Func<DifficultySettings, (bool, UINode)> continuation = null!;
    protected override int NumOptions => dfcDisplays.Count;
    protected override int DefaultOption => dfcDisplays.IndexOf(x => x.key == FixedDifficulty.Normal);

    public UIScreen Initialize(XMLMainMenu menu, Func<DifficultySettings, (bool, UINode)> dfcContinuation) {
        continuation = dfcContinuation;
        dfcDisplays = new List<(FixedDifficulty?, TelescopingDisplay)>();
        if (customDifficulty != null)
            dfcDisplays.Add((null, customDifficulty));
        
        foreach (var dfc in difficultyDisplays)
            dfcDisplays.Add((dfc.dfc, dfc.display));

        return base.Initialize(menu);
    }

    protected override void HideOnExit() {
        dfcDisplays.ForEach(x => x.display.Show(false));
    }

    protected override void Show(int index, bool isOnEnter) {
        if (commentator == null) {
            dfcDisplays.ForEachI((i, x) => {
                x.display.Show(true);
                x.display.SetRelative(Vector2.zero, new Vector2(2.4f, -1.6f).normalized, i, index, NumOptions, isOnEnter);
            });
        } else {
            commentator.SetDifficulty(dfcDisplays[index].key);
            dfcDisplays.ForEachI((i, x) => {
                x.display.Show(true);
                x.display.SetRelative(new Vector2(-2.9f, 0), new Vector2(1.4f, -2.6f).normalized * 0.7f, i, index, NumOptions, isOnEnter);
            });
        }
    }

    protected override (bool success, UINode? nxt) Activate(int index) {
        if (dfcDisplays[index].key.Try(out var fixedDiff)) {
            return continuation(new DifficultySettings(fixedDiff));
        } else {
            if (Menu is XMLMainMenuCampaign c) {
                return (true, c.CustomDifficultyScreen.Top[0]);
            } else {
                throw new Exception($"No custom difficulty handling coded for menu {Menu.GetType()}");
            }
        }
    }

    protected override void OnPreExit() {
        if (commentator != null)
            commentator.Disappear();
    }
    protected override void OnPreEnter(int index) {
        if (commentator != null) {
            commentator.SetDifficulty(dfcDisplays[index].key);
            commentator.Appear();
        }
    }
}
}