using System.Collections.Generic;
using Danmokou.Behavior;
using UnityEngine.Profiling;

namespace Danmokou.Plugins.Danmokou.Utility {
public class AllocTesting : RegularUpdater {
    public int[] arrayData = new[] { 1, 2, 3 };
    public List<int> listData = new() { 1, 2, 3 };
    public HashSet<int> setData = new() { 1, 2, 3 };
    public Dictionary<int, int> dictData = new() { { 1, 2 }, { 3, 4 }, { 5, 6 } };
    public Stack<int> stackData = new();
    public override void FirstFrame() {
        stackData.Push(1);
        stackData.Push(2);
        stackData.Push(3);
    }

    public override void RegularUpdate() {
        var total = 0;
        Profiler.BeginSample("array foreach");
        foreach (var x in arrayData)
            total += x;
        Profiler.EndSample();
        Profiler.BeginSample("list foreach");
        foreach (var x in listData)
            total += x;
        Profiler.EndSample();
        Profiler.BeginSample("set foreach");
        foreach (var x in setData)
            total += x;
        Profiler.EndSample();
        Profiler.BeginSample("dict foreach");
        foreach (var x in dictData)
            total += x.Key + x.Value;
        Profiler.EndSample();
        Profiler.BeginSample("dictK foreach");
        foreach (var x in dictData.Keys)
            total += x;
        Profiler.EndSample();
        Profiler.BeginSample("dictV foreach");
        foreach (var x in dictData.Values)
            total += x;
        Profiler.EndSample();
        Profiler.BeginSample("stack foreach");
        foreach (var x in stackData)
            total += x;
        Profiler.EndSample();
        Profiler.BeginSample("Params empty");
        total += ParamsTest();
        Profiler.EndSample();
        /*Profiler.BeginSample("Params non-empty");
        total += ParamsTest(1,2,3);
        Profiler.EndSample();*/
    }

    public int ParamsTest(params int[] xs) {
        var total = 0;
        foreach (var x in xs)
            total += x;
        return total;
    }
}
}