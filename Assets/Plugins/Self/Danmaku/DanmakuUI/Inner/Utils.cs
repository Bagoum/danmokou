using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Danmaku.DanmakuUI {
public static class Utils {
    public static void SetClassOfSelected(this List<VisualElement> arr, int selected, string cls) =>
        arr.ForEachI((i, x) => {
            if (i == selected) x.AddToClassList(cls);
            else x.RemoveFromClassList(cls);
        });
}
}