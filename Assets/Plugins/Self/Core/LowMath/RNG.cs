using System;
using System.Linq.Expressions;
using DMath;
using JetBrains.Annotations;
using UnityEngine;
using Random = System.Random;

/// <summary>
/// Centralized class for access to randomness.
/// Provides functions for deterministic randomness, as well as "offFrame" functions that
/// should be used for non-gameplay-related randomness (camera shake, particle effects, etc).  
/// </summary>
public static class RNG {
    private static Random rand = new Random();
    private static int seed;
    //Use this instead when the code is not in the RU loop
    private static readonly Random offFrame = new Random();
    
    // Before starting a replay-recordable section, we need to seed the RNG with a known number, and store that in the replay.
    // However, we definitely don't want to fix the RNG for the replay-recordable section. Here is the easy solution.
    // Note this is not cryptographically secure, etc etc. 
    public static int Reseed() {
        seed = rand.Next();
        rand = new Random(seed);
        return seed;
    }

    public static uint GetUInt() {
        return (((uint) rand.Next(1 << 30)) << 2) | (uint) rand.Next(1 << 2) ;
    }

    public const uint HalfMax = uint.MaxValue / 2;

    private const long knuth = 2654435761;
    public static uint Rehash(uint x) => (uint)(knuth * x);
    public static Expression Rehash(Expression uintx) => Expression.Convert(Expression.Multiply(Expression.Constant(knuth), Expression.Convert(uintx, typeof(long))), typeof(uint));

    public static int Rehash(int x) => (int) (knuth * x);

    private static int GetInt(int low, int high, Random r) {
        return r.Next(low, high);
    }

    /// <summary>
    /// Return a random integer.
    /// </summary>
    /// <param name="low">Minimum number (inclusive)</param>
    /// <param name="high">Maximum number (exclusive)</param>
    /// <returns></returns>
    public static int GetInt(int low, int high) => GetInt(low, high, rand);
    public static int GetIntOffFrame(int low, int high) => GetInt(low, high, offFrame);
    private static float GetFloat(float low, float high, Random r) {
        return low + (high - low) * r.Next() / int.MaxValue;
    }

    public static float GetFloat(float low, float high) => GetFloat(low, high, rand);

    public static Vector2 GetPointInCircle(float lowR, float highR) =>
        GetFloat(lowR, highR) * M.RadToDir(GetFloat(0, M.TAU));
    
    private static readonly ExFunction getFloat = ExUtils.Wrap(typeof(RNG), "GetFloat", new[] {typeof(float), typeof(float)});
    public static Expression GetFloat(Expression low, Expression high) => getFloat.Of(low, high);
    public static float GetFloatOffFrame(float low, float high) => GetFloat(low, high, offFrame);
    [UsedImplicitly]
    public static float GetSeededFloat(float low, float high, uint seedv) {
        return low + (high - low) * Rehash(seedv) / uint.MaxValue;
    }
    [UsedImplicitly]
    public static float GetSeededFloat(float low, float high, int seedv) {
        return low + (high - low) * (0.5f + ((float)Rehash(seedv) / int.MaxValue));
    }

    private const string CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    public static string RandString(int len=8) {
        var stringChars = new char[len];
        for (int i = 0; i < stringChars.Length; i++) {
            stringChars[i] = CHARS[offFrame.Next(CHARS.Length)];
        }
        return new string(stringChars);
    }
}
