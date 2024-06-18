using System;
using BagoumLib;
using BagoumLib.DataStructures;
using Danmokou.UI.XML;

/// <summary>
/// Callback for when a link is clicked in a UI handler.
/// </summary>
public delegate (bool success, UIGroup? tooltip) LinkClicked(string id);

namespace Danmokou.UI {
public static class LinkCallback {
    private static readonly DMCompactingArray<LinkClicked> clickers = new(16);

    /// <summary>
    /// Register a callback that handles links clicked in UI handlers.
    /// </summary>
    public static IDisposable RegisterClicker(LinkClicked cb) => clickers.Add(cb);

    public static IDisposable RegisterClicker(params (string id, string text)[] pairs) => RegisterClicker(id => {
        for (int ii = 0; ii < pairs.Length; ++ii) {
            var (linkId, ttText) = pairs[ii];
            if (linkId == id) {
                return (true, ServiceLocator.Find<XMLDynamicMenu>().MakeTooltip(UINode.SimpleTTGroup(ttText),
                    (_, ve) => ve.AddToClassList("tooltip-above")));
            }
        }
        return (false, null);
    });

    public static (bool success, UIGroup? tooltip) ProcessClick(string id) {
        for (int ii = 0; ii < clickers.Count; ++ii)
            if (clickers.GetIfExistsAt(ii, out var clicker) && clicker(id) is { success: true } res)
                return res;
        return (false, null);
    }
}
}