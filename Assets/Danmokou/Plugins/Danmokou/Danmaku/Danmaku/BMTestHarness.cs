
namespace Danmokou.Danmaku {
public partial class BulletManager {
    #if UNITY_EDITOR
    public static SimpleBulletCollection TPool(string pool) => GetMaybeCopyPool(pool);

#endif
}
}