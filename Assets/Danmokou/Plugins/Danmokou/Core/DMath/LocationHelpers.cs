using System.Runtime.CompilerServices;
using Danmokou.Core;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;

namespace Danmokou.DMath {
public static class LocationHelpers {
    public static Vector2 PlayableScreenCenter { get; private set; } = Vector2.zero;
    public static FieldBounds PlayableBounds { get; private set; } = new() {
        left = -3.6f,
        right = 3.6f,
        top = 4.1f,
        bot = -4.5f
    };
    public static Vector2 VisiblePlayerLocation { get; private set; }
    public static Vector2 TruePlayerLocation { get; private set; }

    public static void UpdatePlayableScreenCenter(Vector2 nLoc) {
        PlayableScreenCenter = nLoc;
        Left = nLoc.x + PlayableBounds.left;
        Right = nLoc.x + PlayableBounds.right;
        Top = nLoc.y + PlayableBounds.top;
        Bot = nLoc.y + PlayableBounds.bot;
    }

    public static void UpdateBounds(FieldBounds bounds) {
        PlayableBounds = bounds;
        UpdatePlayableScreenCenter(PlayableScreenCenter);
    }

    public static void UpdatePlayerLocation(Vector2 trueLoc, Vector2 enemyVisibleLoc) {
        TruePlayerLocation = trueLoc;
        VisiblePlayerLocation = enemyVisibleLoc;
    }

    static LocationHelpers() {
        UpdatePlayableScreenCenter(Vector2.zero);
    }
    public static float Left { get; private set; }
    public static float LeftMinus1 => Left - 1;
    public static float Right { get; private set; }
    public static float RightPlus1 => Right + 1;
    public static float Bot { get; private set; }
    public static float BotMinus1 => Bot - 1;
    public static float Top { get; private set; }
    public static float TopPlus1 => Top + 1;
    public static float Width => Right - Left;
    public static float Height => Top - Bot;
    public static float LeftPlayerBound => Left + 0.1f;
    public static float RightPlayerBound => Right - 0.1f;
    public static float BotPlayerBound => Bot + 0.42f;
    public static float TopPlayerBound => Top - 0.1f;
    public static readonly Ex left = Ex.Property(null, typeof(LocationHelpers), "Left");
    public static readonly Ex right = Ex.Property(null, typeof(LocationHelpers), "Right");
    public static readonly Ex bot = Ex.Property(null, typeof(LocationHelpers), "Bot");
    public static readonly Ex top = Ex.Property(null, typeof(LocationHelpers), "Top");
    public static readonly Ex width = Ex.Property(null, typeof(LocationHelpers), "Width");
    public static readonly Ex height = Ex.Property(null, typeof(LocationHelpers), "Height");

    /// <summary>
    /// Assumes that v2 is in bounds. Dir need not be normalized.
    /// </summary>
    public static Vector2 ToWall(Vector2 from, Vector2 dir) {
        float lrintercept = float.MaxValue;
        float udintercept = float.MaxValue;
        if (dir.x > 0) {
            lrintercept = (Right - from.x) / dir.x * dir.y + from.y;
        } else if (dir.x < 0) {
            lrintercept = (Left - from.x) / dir.x * dir.y + from.y;
        }
        if (dir.y > 0) {
            udintercept = (Top - from.y) / dir.y * dir.x + from.x;
        } else if (dir.y < 0) {
            udintercept = (Bot - from.y) / dir.y * dir.x + from.x;
        }
        if (lrintercept > Bot && lrintercept < Top) {
            return new Vector2((dir.x > 0) ? Right : Left, lrintercept);
        }
        if (udintercept > Left && udintercept < Right) {
            return new Vector2(udintercept, (dir.y > 0) ? Top : Bot);
        }
        return Vector2.zero;
    }

    /// <summary>
    /// Assumes that v2 is in bounds. Dir need not be normalized.
    /// </summary>
    public static float DistToWall(Vector2 from, Vector2 dir) {
        return (from - ToWall(from, dir)).magnitude;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool OnPlayableScreen(Vector2 loc) {
        return (loc.x >= Left && loc.x <= Right && loc.y >= Bot && loc.y <= Top);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool OnPlayableScreenBy(float f, Vector2 loc) {
        return (loc.x >= Left - f && loc.x <= Right + f && loc.y >= Bot - f && loc.y <= Top + f);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool OffPlayableScreenBy(float f, Vector2 loc) {
        return (loc.x < Left - f || loc.x > Right + f || loc.y < Bot - f || loc.y > Top + f);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool OffPlayableScreenBy(in float f, in Vector3 loc) {
        return (loc.x < Left - f || loc.x > Right + f || loc.y < Bot - f || loc.y > Top + f);
    }

    public static bool OnScreenInDirection(Vector2 loc, Vector2 directedOffset) {
        if      (directedOffset.y < 0) 
            return loc.y > Bot + directedOffset.y;
        else if (directedOffset.y > 0) 
            return loc.y < Top + directedOffset.y;
        else if (directedOffset.x < 0) 
            return loc.x > Left + directedOffset.x;
        else if (directedOffset.x > 0) 
            return loc.x < Right + directedOffset.x;
        return false;
    }
}
}