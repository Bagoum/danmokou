
namespace Danmokou.Danmaku {
public partial class BulletManager {
    #if UNITY_EDITOR
    public static AbsSimpleBulletCollection TPool(string pool) => GetMaybeCopyPool(pool);

#endif
}
}