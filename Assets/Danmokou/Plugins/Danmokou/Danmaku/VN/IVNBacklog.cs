using BagoumLib.Cancellation;
using SuzunoyaUnity;

namespace Danmokou.VN {
public interface IVNBacklog {
    /// <summary>
    /// Register an executing VN to push its messages into this backlog.
    /// </summary>
    public void TryRegister(ExecutingVN evn);

    public void QueueOpen();
}
}