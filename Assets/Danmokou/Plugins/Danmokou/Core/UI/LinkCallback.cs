using System;
using BagoumLib;
using BagoumLib.DataStructures;
using Danmokou.UI.XML;

/// <summary>
/// Callback for when a link is clicked in a UI handler.
/// </summary>
public delegate (bool success, TooltipProxy? tooltip) LinkEvent(string id);

namespace Danmokou.UI {
public static class LinkCallback {
    private static readonly DMCompactingArray<LinkEvent> clickers = new(16);
    private static readonly DMCompactingArray<LinkEvent> hoverers = new(16);

    private static LinkEvent MakeTooltipMap(params (string id, string text)[] pairs) => id => {
        for (int ii = 0; ii < pairs.Length; ++ii) {
            var (linkId, ttText) = pairs[ii];
            if (linkId == id) {
                return (true, ServiceLocator.Find<XMLDynamicMenu>().MakeTooltip(UINode.SimpleTTGroup(ttText),
                    (_, ve) => ve.AddToClassList("tooltip-above")));
            }
        }
        return (false, null);
    };

    /// <summary>
    /// Register a callback that handles links clicked in UI handlers.
    /// </summary>
    public static IDisposable RegisterClicker(LinkEvent cb) => clickers.Add(cb);

    /// <inheritdoc cref="RegisterClicker(LinkEvent)"/>
    public static IDisposable RegisterClicker(params (string id, string text)[] pairs) => 
        RegisterClicker(MakeTooltipMap(pairs));
    /// <summary>
    /// Register a callback that handles links clicked in UI handlers.
    /// </summary>
    public static IDisposable RegisterHoverer(LinkEvent cb) => hoverers.Add(cb);

    /// <inheritdoc cref="RegisterHoverer(LinkEvent)"/>
    public static IDisposable RegisterHoverer(params (string id, string text)[] pairs) => 
        RegisterHoverer(MakeTooltipMap(pairs));

    private static (bool success, TooltipProxy? tooltip) Process(DMCompactingArray<LinkEvent> handlers, string id) {
        for (int ii = 0; ii < handlers.Count; ++ii)
            if (handlers.GetIfExistsAt(ii, out var clicker) && clicker(id) is { success: true } res)
                return res;
        return (false, null);
    }

    public static (bool success, TooltipProxy? tooltip) ProcessClick(string id) => Process(clickers, id);
    public static (bool success, TooltipProxy? tooltip) ProcessHover(string id) => Process(hoverers, id);
}
}