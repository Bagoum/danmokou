using BagoumLib.Cancellation;
using SuzunoyaUnity;

namespace Danmokou.VN {
public interface IVNBacklog {
    public Cancellable? TryRegister(ExecutingVN evn);

    public void Open();
}
}