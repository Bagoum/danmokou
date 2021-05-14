using Danmokou.Services;

namespace Danmokou.Behavior {
public abstract class ProcReader : RegularUpdater {
    public int checkEveryFrames;
    private int procCt;

    public enum ProcMethod {
        Graze,
    }

    public ProcMethod method;

    public sealed override void RegularUpdate() {
        procCt += GetCurrentFrame();
        if (Counter.FrameNumber % checkEveryFrames == 0) {
            Check(procCt);
            procCt = 0;
        }
    }

    protected void ResetProcs() => procCt = 0;

    private int GetCurrentFrame() {
        if (method == ProcMethod.Graze) return Counter.GrazeFrame;
        return 0;
    }

    protected abstract void Check(int procs);
}
}