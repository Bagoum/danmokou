using Danmokou.Player;
using Danmokou.Scriptables;

namespace Danmokou.Danmaku.Descriptors {

public readonly struct PlayerBullet {
    public readonly PlayerBulletCfg data;
    public readonly PlayerController? firer;

    public PlayerBullet(PlayerBulletCfg data, PlayerController? firer) {
        this.data = data;
        this.firer = firer;
    }
}
public readonly struct PlayerBulletCfg {
    public readonly int cdFrames;
    public readonly bool destructible;
    public readonly int bossDmg;
    public readonly int stageDmg;

    public PlayerBulletCfg(int cd, bool destructible, int boss, int stage) {
        cdFrames = cd;
        this.destructible = destructible;
        bossDmg = boss;
        stageDmg = stage;
    }

    public PlayerBullet Realize(PlayerController firer) => new(this, firer);
}
}