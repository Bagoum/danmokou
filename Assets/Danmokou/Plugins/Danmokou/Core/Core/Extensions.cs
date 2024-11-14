using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.Functional;
using Danmokou.DMath;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Danmokou.Core {
public static class Extensions {
    public static Maybe<T> ToMaybe<T>(this T? obj) where T : Object {
        return obj == null ? Maybe<T>.None : obj;
    }
    public static int CountOf(this string s, char c) {
        int ct = 0;
        for (int ii = 0; ii < s.Length; ++ii) {
            if (s[ii] == c) ++ct;
        }
        return ct;
    }

    public static void Show(this VisualElement ve) => ve.style.display = DisplayStyle.Flex;
    public static void Hide(this VisualElement ve) => ve.style.display = DisplayStyle.None;
}

public static class FormattingExtensions {
    public static string PadRight(this int x, int by) => x.ToString().PadRight(by);
    public static string PadLZero(this int x, int by) => x.ToString().PadLeft(by, '0');

    public static string SimpleDate(this DateTime d) =>
        $"{d.Year}/{d.Month.PadLZero(2)}/{d.Day.PadLZero(2)}";
    public static string SimpleTime(this DateTime d) =>
        $"{d.SimpleDate()} " +
        $"{d.Hour.PadLZero(2)}:{d.Minute.PadLZero(2)}:{d.Second.PadLZero(2)}";

    public static string FileableTime(this DateTime d) =>
        $"{d.Year} {d.Month.PadLZero(2)} {d.Day.PadLZero(2)} " +
        $"{d.Hour.PadLZero(2)} {d.Minute.PadLZero(2)} {d.Second.PadLZero(2)}";
}

public static class NumExtensions {
    public static string AsDelta(this int x) => (x < 0) ? $"{x:N0}" : $"+{x:N0}";

    public static char ToABC(this int x) => (char) ('A' + x);

    public static LString FramesToTime(this int f) {
        int s = (int) (f / ETime.ENGINEFPS_F);
        int hours = s / 3600;
        s %= 3600;
        int minutes = s / 60;
        s %= 60;
        int seconds = s;
        return hours > 0 ? 
            LocalizedStrings.Generic.render_hoursminssecs_ls(hours, minutes, seconds) : 
            LocalizedStrings.Generic.render_minssecs_ls(minutes, seconds);
    }
}

public static class UnityExtensions {
    public static T Elvis<T>(this T? x, T y) where T : UnityEngine.Object
        => (x == null) ? y : x;

    public static void SetAlpha(this SpriteRenderer sr, float a) {
        var c = sr.color;
        c.a = a;
        sr.color = c;
    }

    /// <summary>
    /// Return the pivot of a sprite in (0, 1) coordinates.
    /// </summary>
    public static Vector2 Pivot(this Sprite s) {
        var p = s.pivot;
        p.x /= s.rect.width;
        p.y /= s.rect.height;
        return p;
    }

    /// <summary>
    /// Return the dimensions of a sprite in Unity coordinates.
    /// </summary>
    public static Vector2 Dims(this Sprite s) {
        return new Vector2(s.rect.width / s.pixelsPerUnit, s.rect.height / s.pixelsPerUnit);
    }
}

}