using System;
using UnityEngine;

public interface IProccable {
    void Proc(int ct);
}

public interface ICtProccable: IProccable {
    int Count();
    void Reset();
}
public abstract class SOProccable : ScriptableObject, IProccable {
    [NonSerialized] protected int procs;
    public abstract void Proc(int ct=1);
}

[CreateAssetMenu(menuName = "Utility/Proccable")]
public class SOCtProccable : SOProccable, ICtProccable {

    public override void Proc(int ct=1) {
        procs += ct;
    }
    public int Count() {
        return procs;
    }
    public int CountReset() {
        var p = procs;
        procs = 0;
        return p;
    }

    public void Reset() {
        procs = 0;
    }
}