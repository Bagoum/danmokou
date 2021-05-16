using Danmokou.Player;
using Danmokou.Scriptables;

namespace Danmokou.Danmaku.Descriptors {

public readonly struct PlayerBullet {
    public readonly PlayerBulletCfg data;
    public readonly PlayerController firer;

    public PlayerBullet(PlayerBulletCfg data, PlayerController firer) {
        this.data = data;
        this.firer = firer;
    }
}
public readonly struct PlayerBulletCfg {
    public readonly int cdFrames;
    public readonly int bossDmg;
    public readonly int stageDmg;
    public readonly EffectStrategy effect;

    public PlayerBulletCfg(int cd, int boss, int stage, EffectStrategy eff) {
        cdFrames = cd;
        bossDmg = boss;
        stageDmg = stage;
        effect = eff;
    }

    public PlayerBullet Realize(PlayerController firer) => new PlayerBullet(this, firer);
}
}