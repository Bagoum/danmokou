using System;
using CommunityToolkit.HighPerformance.Buffers;

namespace Danmokou.Core {
public static class StringAllocator {
    private static readonly StringPool allocator = new();
    public const bool USE_POOLED_ALLOC = true;

    /// <summary>
    /// Create a cached string from a char array.
    /// </summary>
    /// <param name="chars">Character array</param>
    /// <param name="start">Character range start (inclusive)</param>
    /// <param name="end">Character range end (exclusive)</param>
    /// <returns>Result string (may be pooled)</returns>
    public static string MakeString(this char[] chars, int start, int end) =>
        //Span doesn't work in the language server since it got moved to a different repository in NET Core
        // as such, when building for language server, use new string
        USE_POOLED_ALLOC ?
            allocator.GetOrAdd(new ReadOnlySpan<char>(chars, start, end)) :
            new string(chars, start, end);
    
    public static string MakeString(this ReadOnlySpan<char> chars) =>
        //Span doesn't work in the language server since it got moved to a different repository in NET Core
        // as such, when building for language server, use new string
        USE_POOLED_ALLOC ?
            allocator.GetOrAdd(chars) :
            new string(chars);

    /// <summary>
    /// Create a cached string from a char.
    /// </summary>
    public static unsafe string MakeString(this char c) =>
        USE_POOLED_ALLOC ?
            allocator.GetOrAdd(new ReadOnlySpan<char>(&c, 1)) :
            c.ToString();

}
}