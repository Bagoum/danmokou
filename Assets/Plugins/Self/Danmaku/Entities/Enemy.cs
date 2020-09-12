using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DMath;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using static Danmaku.Enums;

namespace Danmaku {
public class Enemy : RegularUpdater {
    public readonly struct FrozenCollisionInfo {
        public readonly Vector2 pos;
        public readonly float radius;
        public readonly int enemyIndex;
        public bool Active => allEnemies.ContainsKey(enemyIndex);
        public readonly Enemy enemy;

        public FrozenCollisionInfo(Enemy e) {
            pos = e.beh.GlobalPosition();
            radius = e.collisionRadius;
            enemy = e;
            enemyIndex = e.enemyIndex;
        }
    }

    private BehaviorEntity beh;
    public bool takesBossDamage;
    [CanBeNull] private (bool _, Enemy to)? divertHP = null;
    public int HP { get; private set; }
    public int maxHP = 1000;
    public bool Vulnerable { get; private set; }= true;
    //private static int enemyIndexCtr = 0;
    //private int enemyIndex;

    public RFloat collisionRadius;

    private const float LOW_HP_THRESHOLD = .2f;

    public bool modifyDamageSound;

    [Header("Healthbar Controller (Optional)")] [CanBeNull]
    public SpriteRenderer healthbarSprite;
    private MaterialPropertyBlock hpPB;
    public SpriteRenderer cardCircle;
    public SpriteRenderer spellCircle;
    private Transform cardtr;
    [CanBeNull] public SpriteRenderer distorter;
    private MaterialPropertyBlock distortPB;
    private MaterialPropertyBlock scPB;
    private float healthbarStart; // 0-1
    private float healthbarSize; //As fraction of total bar, 0-1

    public RColor2 nonspellColor;
    public RColor2 spellColor;

    public RColor unfilledColor;

    public RFloat hpRadius;
    public RFloat hpThickness;

    //Previously 6f, increasing for fire shader
    private const float HPLerpRate = 14f;

    private BPY cardRotator = _ => 60;

    private static short renderCounter = short.MinValue;
    
    public ItemDrops AutoDeathItems => new ItemDrops(
        Mathf.CeilToInt(maxHP/1500f), 
        maxHP >= 500 ? Mathf.CeilToInt(maxHP/2000f) : 0, 
        maxHP >= 500 ? Mathf.CeilToInt(maxHP/1000f) : 0,
        maxHP >= 1000 ? Mathf.CeilToInt(maxHP / 1200f) : 0
        );


    private static int enemyIndexCtr = 0;
    private int enemyIndex;
    private static readonly Dictionary<int, Enemy> allEnemies = new Dictionary<int, Enemy>();
    private static readonly List<FrozenCollisionInfo> frozenEnemies = new List<FrozenCollisionInfo>();
    public static IReadOnlyList<FrozenCollisionInfo> FrozenEnemies => frozenEnemies;

    public void Initialize(BehaviorEntity _beh, [CanBeNull] SpriteRenderer sr) {
        beh = _beh;
        var sortOrder = renderCounter++;
        if (sr != null) sr.sortingOrder = sortOrder;
        if (spellCircle != null) spellCircle.sortingOrder = sortOrder;
        if (cardCircle != null) cardCircle.sortingOrder = sortOrder;
        if (healthbarSprite != null) healthbarSprite.sortingOrder = sortOrder;
        allEnemies[enemyIndex = enemyIndexCtr++] = this;
        HP = maxHP;
        queuedDamage = 0;
        Vulnerable = true;
        hpPB = new MaterialPropertyBlock();
        distortPB = new MaterialPropertyBlock();
        scPB = new MaterialPropertyBlock();
        if (healthbarSprite != null) {
            healthbarSprite.enabled = true;
            healthbarSprite.GetPropertyBlock(hpPB);
            hpPB.SetFloat(PropConsts.radius, hpRadius);
            hpPB.SetFloat(PropConsts.subradius, hpThickness);
            healthbarStart = 0f;
            healthbarSize = 1f;
            SetHPBarColors(PhaseType.NONSPELL);
            hpPB.SetColor(PropConsts.unfillColor, unfilledColor);
            _displayHPRatio = HPRatio;
            hpPB.SetFloat(PropConsts.fillRatio, DisplayHPRatio);
            hpPB.SetColor(PropConsts.fillColor, currPhase.color1);
            healthbarSprite.SetPropertyBlock(hpPB);
        }
        if (distorter != null) {
            distorter.GetPropertyBlock(distortPB);
            distortPB.SetFloat(PropConsts.time, 0f);
            distorter.SetPropertyBlock(distortPB);
            cardtr = cardCircle.transform;
            cardCircle.enabled = false;
            distorter.enabled = false;
        }
        if (spellCircle != null) {
            spellCircle.GetPropertyBlock(scPB);
            scPB.SetFloat(PropConsts.time, 0f);
            scPB.SetFloat(PropConsts.radius, lastSpellCircleRadius);
            spellCircle.SetPropertyBlock(scPB);
            spellCircle.enabled = false;
        }
        lastSpellCircleRadius = MinSCRadius;
    }

    public void SetSpellCircleColors(Color c1, Color c2, Color c3) {
        scPB.SetColor(PropConsts.color1, c1);
        scPB.SetColor(PropConsts.color2, c2);
        scPB.SetColor(PropConsts.color3, c3);
    }

    public void RequestCardCircle(Color colorR, Color colorG, Color colorB, BPY rotator) {
        if (cardCircle != null) {
            cardCircle.enabled = true;
            if (distorter != null) distorter.enabled = SaveData.s.Shaders;
            var cpb = new MaterialPropertyBlock();
            cardCircle.GetPropertyBlock(cpb);
            cpb.SetColor(PropConsts.redColor, colorR);
            cpb.SetColor(PropConsts.greenColor, colorG);
            cpb.SetColor(PropConsts.blueColor,colorB);
            cardCircle.SetPropertyBlock(cpb);
            cardRotator = rotator;
        }
    }

    private void RecheckGraphicsSettings() {
        if (cardCircle != null && distorter != null) {
            distorter.enabled = cardCircle.enabled & SaveData.s.Shaders;
        }
    }

    private ICancellee spellCircleCancel = Cancellable.Null;
    [CanBeNull] private FXY spellCircleRadiusFunc;
    private float MinSCRadius => hpRadius + hpThickness;
    private float lastSpellCircleRadius;
    private const float SpellCircleLerpTime = 0.6f;
    private const float SCBREATHMAG = 0.15f;
    private const float SCBREATHPER = 5f;
    public void RequestSpellCircle(float timeout, ICancellee cT, float startRad=3f) {
        if (timeout < 0.1) timeout = M.IntFloatMax;
        if (spellCircle == null) return;
        spellCircleCancel = cT;
        spellCircle.enabled = true;
        float baseT = beh.rBPI.t;
        float baseRad = Math.Max(MinSCRadius, lastSpellCircleRadius);
        spellCircleRadiusFunc = t => {
            if (t < baseT + SpellCircleLerpTime) {
                return Mathf.Lerp(baseRad, startRad, (t - baseT) / SpellCircleLerpTime);
            }
            float pt = t - baseT - SpellCircleLerpTime;
            return Mathf.Max(MinSCRadius, 
                Mathf.Lerp(startRad, MinSCRadius, pt / timeout) *
                   (1 + SCBREATHMAG * Mathf.Sin(M.TAU * pt / SCBREATHPER)));
        };
        RecheckGraphicsSettings();
    }

    public void DivertHP(Enemy to) => divertHP = (false, to);
    private float HPRatio => (float) HP / maxHP;
    private float _displayHPRatio;
    public float EffectiveHPRatio => divertHP?.to.EffectiveHPRatio ?? HPRatio;
    public float DisplayHPRatio => divertHP?.to.DisplayHPRatio ?? _displayHPRatio;

    public Color HPColor => Color.Lerp(currPhase.color2, currPhase.color1, Mathf.Pow(_displayHPRatio, 1.5f));
    public override void RegularUpdate() {
        PollDamage();
        if (healthbarSprite != null) {
            _displayHPRatio = Mathf.Lerp(_displayHPRatio, HPRatio, HPLerpRate * ETime.FRAME_TIME);
            hpPB.SetFloat(PropConsts.fillRatio, DisplayHPRatio);
            //Approximation to make the max color appear earlier
            hpPB.SetColor(PropConsts.fillColor, HPColor);
            hpPB.SetFloat(PropConsts.time, beh.rBPI.t);
            healthbarSprite.SetPropertyBlock(hpPB);
        }
        if (distorter != null) {
            distortPB.SetFloat(PropConsts.time, beh.rBPI.t);
            MainCamera.SetPBScreenLoc(distortPB, beh.GlobalPosition());
            distorter.SetPropertyBlock(distortPB);
        }
        if (cardCircle != null) {
            Vector3 rt = cardtr.localEulerAngles;
            rt.z += ETime.FRAME_TIME * cardRotator(beh.rBPI);
            cardtr.localEulerAngles = rt;
        }
        if (spellCircle != null) {
            scPB.SetFloat(PropConsts.time, beh.rBPI.t);
            if (spellCircleCancel.Cancelled) {
                float baseT = beh.rBPI.t;
                spellCircleRadiusFunc = t => Mathf.Lerp(lastSpellCircleRadius, MinSCRadius - 0.1f, (t - baseT) / SpellCircleLerpTime);
                spellCircleCancel = Cancellable.Null;
            }
            lastSpellCircleRadius = spellCircleRadiusFunc?.Invoke(beh.rBPI.t) ?? lastSpellCircleRadius;
            if (lastSpellCircleRadius < MinSCRadius) spellCircle.enabled = false;
            scPB.SetFloat(PropConsts.radius, lastSpellCircleRadius);
            spellCircle.SetPropertyBlock(scPB);
        }
        for (int ii = 0; ii < hitCooldowns.Count; ++ii) {
            if (hitCooldowns[ii].Cooldown == 0) hitCooldowns.Delete(ii);
            else hitCooldowns.arr[ii].obj.Cooldown = hitCooldowns[ii].Cooldown - 1;
        }
        hitCooldowns.Compact();
    }

    public static void FreezeEnemies() {
        frozenEnemies.Clear();
        foreach (var enemy in allEnemies.Values) {
            if (LocationService.OnPlayableScreenBy(0.5f, enemy.beh.GlobalPosition())) {
                frozenEnemies.Add(new FrozenCollisionInfo(enemy));
            }
        }
    }

    private const float SHOTGUN_DIST_MAX = 1f;
    private const float SHOTGUN_DIST_MIN = 2f;
    private const float SHOTGUN_DIST_MAX_BOSS = 1.5f;
    private const float SHOTGUN_DIST_MIN_BOSS = 3f;
    private float SHOTGUN_MAX => takesBossDamage ? SHOTGUN_DIST_MAX_BOSS : SHOTGUN_DIST_MAX;
    private float SHOTGUN_MIN => takesBossDamage ? SHOTGUN_DIST_MIN_BOSS : SHOTGUN_DIST_MIN;
    private const float SHOTGUN_MULTIPLIER = 1.2f;

    private float queuedDamage = 0;

    //The reason we queue damage is to avoid calling eg. SM clear effects while in the middle of other entities' update loops.
    public void QueueDamage(int bossDmg, int stageDmg, Vector2 firerLoc) => 
        QueueDamage(takesBossDamage ? bossDmg : stageDmg, firerLoc);

    private void QueueDamage(int dmg, Vector2 firerLoc) {
        if (divertHP != null) {
            divertHP.Value.to.QueueDamage(dmg, firerLoc);
            return;
        }
        if (!Vulnerable) return;
        float dstToFirer = (firerLoc - beh.rBPI.loc).magnitude;
        float shotgun = (SHOTGUN_MIN - dstToFirer) / (SHOTGUN_MIN - SHOTGUN_MAX);
        float multiplier =
            Mathf.Lerp(1f, 1.2f, shotgun);
        queuedDamage += dmg * multiplier;
        Counter.Shotgun(shotgun);
    }
    private void PollDamage() {
        if (queuedDamage < 1) return;
        HP = M.Clamp(0, maxHP, HP - (int)queuedDamage);
        queuedDamage = 0;
        if (HP == 0) {
            beh.OutOfHP();
            Vulnerable = false; //Wait for new hp value to be declared
        } else if (modifyDamageSound) {
            if ((float) HP / maxHP < LOW_HP_THRESHOLD) {
                Counter.AlertLowEnemyHP();
            }
        }
    }

    public void SetHP(int newMaxHP, int newCurrHP) {
        maxHP = newMaxHP;
        HP = newCurrHP;
    }

    public void SetDamageable(bool isDamageable) {
        Vulnerable = isDamageable;
    }

    private Color2 currPhase;
    private Color2 CardToColor(PhaseType st) {
        return st.IsSpell() ? spellColor : nonspellColor;
    }
    private void SetHPBarColors(PhaseType st) {
        currPhase = CardToColor(st);
        hpPB.SetColor(PropConsts.R2NColor, CardToColor(st.Invert()).color1);
        hpPB.SetFloat(PropConsts.R2CPhaseStart, healthbarStart + healthbarSize);
        hpPB.SetFloat(PropConsts.R2NPhaseStart, healthbarStart);
    }

    [CanBeNull] public IReadOnlyList<Enemy> Subbosses { get; set; } = null;
    public void SetHPBar(float portion, PhaseType color) {
        if (healthbarStart < 0.1f || color.RequiresFullHPBar()) {
            healthbarStart = 1f;
        } else {
            _displayHPRatio = 1f + _displayHPRatio * healthbarSize / (healthbarStart * portion);
        }
        healthbarSize = healthbarStart * portion;
        healthbarStart -= healthbarSize;
        SetHPBarColors(color);
        if (Subbosses != null) {
            foreach (var e in Subbosses) e.SetHPBar(portion, color);
        }
    }

    //"Slower" than using a dictionary, but there are few enough colliding persistent objects at a time that 
    //it's better to optimize for garbage. 
    private readonly DMCompactingArray<(uint ID, int Cooldown)> hitCooldowns = new DMCompactingArray<(uint, int)>(8);
    public bool TryHitIndestructible(uint id, int cooldownFrames) {
        for (int ii = 0; ii < hitCooldowns.Count; ++ii) {
            if (hitCooldowns[ii].ID == id) return false;
        }
        hitCooldowns.Add((id, cooldownFrames));
        return true;
    }

    public void ProcOnHit(EffectStrategy effect, Vector2 hitLoc) => effect.Proc(hitLoc, beh.GlobalPosition(), collisionRadius);

    private static readonly ReflWrap<VTP> SuicideVTP = (Func<VTP>)"tprot cx 1.6".Into<VTP>;
    public void DoSuicideFire() {
        if (GameManagement.DifficultyCounter < DifficultySet.Hard.Counter()) return;
        var bt = LevelController.DefaultSuicideStyle;
        if (string.IsNullOrWhiteSpace(bt)) bt = "triangle-black/w";
        var angleTo = M.AtanD(BulletManager.PlayerTarget.location - beh.rBPI.loc);
        int numBullets = (GameManagement.DifficultyCounter <= DifficultySet.Lunatic.Counter()) ? 1 : 3;
        for (int ii = 0; ii < numBullets; ++ii) {
            BulletManager.RequestSimple(bt, null, null, new Velocity(SuicideVTP, beh.rBPI.loc, 
                angleTo + (ii - numBullets / 2) * 120f / numBullets), 0, 0, null);
        }
    }
    
#if UNITY_EDITOR
    private void OnDrawGizmos() {
        Handles.color = Color.green;
        var position = transform.position;
        Handles.DrawWireDisc(position, Vector3.forward, collisionRadius);
    }
#endif
    public void IAmDead() {
        if (healthbarSprite != null) healthbarSprite.enabled = false;
        allEnemies.Remove(enemyIndex); 
    }


    public static bool FindNearest(Vector2 source, int? preferredEnemy, out int enemy, out Vector2 position) {
        if (preferredEnemy.Try(out var eid) && allEnemies.TryGetValue(eid, out var pe)) {
            position = pe.beh.GlobalPosition();
            enemy = eid;
            return true;
        }
        bool found = false;
        position = default;
        enemy = default;
        float lastDist = 0f;
        foreach (var e in frozenEnemies) {
            if (e.Active) {
                var dst = (e.pos.x - source.x) * (e.pos.x - source.x) + (e.pos.y - source.y) * (e.pos.y - source.x);
                if (!found || dst < lastDist) {
                    lastDist = dst;
                    found = true;
                    enemy = e.enemyIndex;
                    position = e.pos;
                }
            }
        }
        return found;
    }

    public static readonly ExFunction findNearest = ExUtils.Wrap<Enemy>("FindNearest", new[] {
        typeof(Vector2), typeof(int?), typeof(int).MakeByRefType(), typeof(Vector2).MakeByRefType()
    });
}
}
