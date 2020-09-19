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
using Collision = DMath.Collision;

namespace Danmaku {
public class Enemy : RegularUpdater {
    public readonly struct FrozenCollisionInfo {
        public readonly Vector2 pos;
        public readonly float radius;
        public readonly int enemyIndex;
        public bool Active => allEnemies.ContainsKey(enemyIndex);
        public readonly Enemy enemy;

        public FrozenCollisionInfo(Enemy e) {
            pos = e.Beh.GlobalPosition();
            radius = e.collisionRadius;
            enemy = e;
            enemyIndex = e.enemyIndex;
        }
    }

    public BehaviorEntity Beh { get; private set; }
    public bool takesBossDamage;
    [CanBeNull] private (bool _, Enemy to)? divertHP = null;
    public int HP { get; private set; }
    public int maxHP = 1000;
    public int PhotoHP { get; private set; } = 1;
    private int maxPhotoHP = 1;
    public int PhotosTaken { get; private set; } = 0;
    public bool Vulnerable { get; private set; }= true;
    //private static int enemyIndexCtr = 0;
    //private int enemyIndex;

    public RFloat collisionRadius;
    /// <summary>
    /// The entirety of this circle must be within the viewfinder for a capture to succeed.
    /// </summary>
    [CanBeNull] public RFloat ayaCameraRadius;
    

    private const float LOW_HP_THRESHOLD = .2f;

    public bool modifyDamageSound;

    [CanBeNull] public SpriteRenderer cameraCrosshair;

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
        Mathf.CeilToInt(maxHP / 600f), 
        maxHP >= 400 ? Mathf.CeilToInt(maxHP / 900f) : 0, 
        maxHP >= 300 ? Mathf.CeilToInt(maxHP / 800f) : 0,
        maxHP >= 400 ? Mathf.CeilToInt(maxHP / 700f) : 0,
        Mathf.CeilToInt(maxHP / 300f)
        );


    private static int enemyIndexCtr = 0;
    private int enemyIndex;
    private static readonly Dictionary<int, Enemy> allEnemies = new Dictionary<int, Enemy>();
    private static readonly DMCompactingArray<Enemy> orderedEnemies = new DMCompactingArray<Enemy>();
    private DeletionMarker<Enemy> aliveToken;
    private static readonly List<FrozenCollisionInfo> frozenEnemies = new List<FrozenCollisionInfo>();
    public static IReadOnlyList<FrozenCollisionInfo> FrozenEnemies => frozenEnemies;

    public void Initialize(BehaviorEntity _beh, [CanBeNull] SpriteRenderer sr) {
        Beh = _beh;
        var sortOrder = renderCounter++;
        if (sr != null) sr.sortingOrder = sortOrder;
        if (spellCircle != null) spellCircle.sortingOrder = sortOrder * 3;
        if (cardCircle != null) cardCircle.sortingOrder = sortOrder * 3 + 1;
        if (healthbarSprite != null) healthbarSprite.sortingOrder = sortOrder * 3 + 2;
        allEnemies[enemyIndex = enemyIndexCtr++] = this;
        aliveToken = orderedEnemies.Add(this);
        HP = maxHP;
        queuedDamage = 0;
        Vulnerable = true;
        hpPB = new MaterialPropertyBlock();
        distortPB = new MaterialPropertyBlock();
        scPB = new MaterialPropertyBlock();
        if (cameraCrosshair != null) cameraCrosshair.enabled = false;
        if (healthbarSprite != null) {
            healthbarSprite.enabled = true;
            healthbarSprite.GetPropertyBlock(hpPB);
            hpPB.SetFloat(PropConsts.radius, hpRadius);
            hpPB.SetFloat(PropConsts.subradius, hpThickness);
            healthbarStart = 0f;
            healthbarSize = 1f;
            SetHPBarColors(PhaseType.NONSPELL);
            hpPB.SetColor(PropConsts.unfillColor, unfilledColor);
            _displayBarRatio = BarRatio;
            hpPB.SetFloat(PropConsts.fillRatio, DisplayBarRatio);
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
    
    public void ConfigureBoss(BossConfig b) {
        if (b.colors.cardColorR.a > 0 || b.colors.cardColorG.a > 0 || b.colors.cardColorB.a > 0) {
            RequestCardCircle(b.colors.cardColorR, b.colors.cardColorG, b.colors.cardColorB, b.Rotator);
        }
        SetSpellCircleColors(b.colors.spellColor1, b.colors.spellColor2, b.colors.spellColor3);
        if (cameraCrosshair != null) cameraCrosshair.color = b.colors.uiHPColor;
    }
    private void RequestCardCircle(Color colorR, Color colorG, Color colorB, BPY rotator) {
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
    private void SetSpellCircleColors(Color c1, Color c2, Color c3) {
        scPB.SetColor(PropConsts.color1, c1);
        scPB.SetColor(PropConsts.color2, c2);
        scPB.SetColor(PropConsts.color3, c3);
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
    public void RequestSpellCircle(float timeout, ICancellee cT, float startRad=2.3f) {
        if (timeout < 0.1) timeout = M.IntFloatMax;
        if (spellCircle == null) return;
        spellCircleCancel = cT;
        spellCircle.enabled = true;
        float baseT = Beh.rBPI.t;
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
    private float PhotoRatio => (float) PhotoHP / maxPhotoHP;
    private float BarRatio => Math.Min(PhotoRatio, HPRatio);
    private float _displayBarRatio;
    public float EffectiveBarRatio => divertHP?.to.EffectiveBarRatio ?? BarRatio;
    public float DisplayBarRatio => divertHP?.to.DisplayBarRatio ?? _displayBarRatio;

    public Color HPColor => Color.Lerp(currPhase.color2, currPhase.color1, Mathf.Pow(_displayBarRatio, 1.5f));
    public override void RegularUpdate() {
        PollDamage();
        PollPhotoDamage();
        _displayBarRatio = Mathf.Lerp(_displayBarRatio, BarRatio, HPLerpRate * ETime.FRAME_TIME);
        if (healthbarSprite != null) {
            hpPB.SetFloat(PropConsts.fillRatio, DisplayBarRatio);
            //Approximation to make the max color appear earlier
            hpPB.SetColor(PropConsts.fillColor, HPColor);
            hpPB.SetFloat(PropConsts.time, Beh.rBPI.t);
            healthbarSprite.SetPropertyBlock(hpPB);
        }
        if (distorter != null) {
            distortPB.SetFloat(PropConsts.time, Beh.rBPI.t);
            MainCamera.SetPBScreenLoc(distortPB, Beh.GlobalPosition());
            distorter.SetPropertyBlock(distortPB);
        }
        if (cardCircle != null) {
            Vector3 rt = cardtr.localEulerAngles;
            rt.z += ETime.FRAME_TIME * cardRotator(Beh.rBPI);
            cardtr.localEulerAngles = rt;
        }
        if (spellCircle != null) {
            scPB.SetFloat(PropConsts.time, Beh.rBPI.t);
            if (spellCircleCancel.Cancelled) {
                float baseT = Beh.rBPI.t;
                spellCircleRadiusFunc = t => Mathf.Lerp(lastSpellCircleRadius, MinSCRadius - 0.1f, (t - baseT) / SpellCircleLerpTime);
                spellCircleCancel = Cancellable.Null;
            }
            lastSpellCircleRadius = spellCircleRadiusFunc?.Invoke(Beh.rBPI.t) ?? lastSpellCircleRadius;
            if (lastSpellCircleRadius < MinSCRadius) spellCircle.enabled = false;
            scPB.SetFloat(PropConsts.radius, lastSpellCircleRadius);
            spellCircle.SetPropertyBlock(scPB);
        }
        for (int ii = 0; ii < hitCooldowns.Count; ++ii) {
            if (hitCooldowns[ii].Cooldown <= 1) hitCooldowns.Delete(ii);
            else hitCooldowns.arr[ii].obj.Cooldown = hitCooldowns[ii].Cooldown - 1;
        }
        hitCooldowns.Compact();
    }

    public static void FreezeEnemies() {
        frozenEnemies.Clear();
        orderedEnemies.Compact();
        for (int ii = 0; ii < orderedEnemies.Count; ++ii) {
            var enemy = orderedEnemies[ii];
            if (LocationService.OnPlayableScreenBy(0.5f, enemy.Beh.GlobalPosition())) {
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
    private const float SHOTGUN_MULTIPLIER = 1.25f;

    private float queuedDamage = 0;
    private int queuedPhotoDamage = 0;

    //The reason we queue damage is to avoid calling eg. SM clear effects while in the middle of other entities' update loops.
    public void QueueDamage(int bossDmg, int stageDmg, Vector2 firerLoc) => 
        QueueDamage(takesBossDamage ? bossDmg : stageDmg, firerLoc);

    private void QueueDamage(int dmg, Vector2? firerLoc) {
        if (divertHP != null) {
            divertHP.Value.to.QueueDamage(dmg, firerLoc);
            return;
        }
        if (!Vulnerable) return;
        if (firerLoc.Try(out var floc)) {
            float dstToFirer = (floc - Beh.rBPI.loc).magnitude;
            float shotgun = (SHOTGUN_MIN - dstToFirer) / (SHOTGUN_MIN - SHOTGUN_MAX);
            float multiplier =
                Mathf.Lerp(1f, 1.2f, shotgun);
            queuedDamage += dmg * multiplier;
            Counter.DoShotgun(shotgun);
        } else queuedDamage += dmg;
    }
    private void PollDamage() {
        if (queuedDamage < 1) return;
        HP = M.Clamp(0, maxHP, HP - (int)queuedDamage);
        queuedDamage = 0;
        if (HP == 0) {
            Beh.OutOfHP();
            Vulnerable = false; //Wait for new hp value to be declared
        } else if (modifyDamageSound) {
            if ((float) HP / maxHP < LOW_HP_THRESHOLD) {
                Counter.AlertLowEnemyHP();
            }
        }
    }

    private void PollPhotoDamage() {
        if (queuedPhotoDamage < 1) return;
        PhotosTaken += queuedPhotoDamage;
        PhotoHP = M.Clamp(0, maxPhotoHP, PhotoHP - queuedPhotoDamage);
        queuedPhotoDamage = 0;
        if (PhotoHP == 0) {
            Beh.OutOfHP();
            Vulnerable = false;
        }
    }

    public void SetHP(int newMaxHP, int newCurrHP) {
        maxHP = newMaxHP;
        HP = newCurrHP;
    }
    public void SetPhotoHP(int newMaxHP, int newCurrHP) {
        maxPhotoHP = newMaxHP;
        PhotoHP = newCurrHP;
        PhotosTaken = 0;
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
            _displayBarRatio = 1f + _displayBarRatio * healthbarSize / (healthbarStart * portion);
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

    public void ProcOnHit(EffectStrategy effect, Vector2 hitLoc) => effect.Proc(hitLoc, Beh.GlobalPosition(), collisionRadius);

    private bool ViewfinderHits(CRect viewfinder) => Collision.CircleInRect(Beh.rBPI.loc, ayaCameraRadius, viewfinder);
    public void ShowCrosshairIfViewfinderHits(CRect viewfinder) {
        if (cameraCrosshair != null) {
            cameraCrosshair.enabled = ViewfinderHits(viewfinder);
        }
    }

    public void HideViewfinderCrosshair() {
        if (cameraCrosshair != null) cameraCrosshair.enabled = false;
    }
    public bool FireViewfinder(CRect viewfinder) {
        HideViewfinderCrosshair();
        if (ViewfinderHits(viewfinder) && Vulnerable) {
            queuedPhotoDamage += 1;
            return true;
        } else return false;
    }

    private static readonly ReflWrap<VTP> SuicideVTP = (Func<VTP>)"tprot cx 1.6".Into<VTP>;
    public void DoSuicideFire() {
        /*
        if (GameManagement.DifficultyCounter < DifficultySet.Hard.Counter()) return;
        var bt = LevelController.DefaultSuicideStyle;
        if (string.IsNullOrWhiteSpace(bt)) bt = "triangle-black/w";
        var angleTo = M.AtanD(BulletManager.PlayerTarget.location - Beh.rBPI.loc);
        int numBullets = (GameManagement.DifficultyCounter <= DifficultySet.Lunatic.Counter()) ? 1 : 3;
        for (int ii = 0; ii < numBullets; ++ii) {
            BulletManager.RequestSimple(bt, null, null, new Velocity(SuicideVTP, Beh.rBPI.loc, 
                angleTo + (ii - numBullets / 2) * 120f / numBullets), 0, 0, null);
        }*/
    }
    
#if UNITY_EDITOR
    private void OnDrawGizmos() {
        var position = transform.position;
        Handles.color = Color.red;
        if (ayaCameraRadius != null) Handles.DrawWireDisc(position, Vector3.forward, ayaCameraRadius);
        Handles.color = Color.green;
        Handles.DrawWireDisc(position, Vector3.forward, collisionRadius);
    }
#endif
    
    //This is called through InvokeCull on BehaviorEntity.OnDisable
    public void IAmDead() {
        if (healthbarSprite != null) healthbarSprite.enabled = false;
        allEnemies.Remove(enemyIndex); 
        aliveToken.MarkForDeletion();
    }

    [UsedImplicitly]
    public static bool FindNearest(Vector2 source, out Vector2 position) {
        bool found = false;
        position = default;
        float lastDist = 0f;
        foreach (var e in frozenEnemies) {
            if (e.Active) {
                var dst = (e.pos.x - source.x) * (e.pos.x - source.x) + (e.pos.y - source.y) * (e.pos.y - source.x);
                if (!found || dst < lastDist) {
                    lastDist = dst;
                    found = true;
                    position = e.pos;
                }
            }
        }
        return found;
    }

    [UsedImplicitly]
    public static bool FindNearestSave(Vector2 source, int? preferredEnemy, out int enemy, out Vector2 position) {
        if (preferredEnemy.Try(out var eid) && allEnemies.TryGetValue(eid, out var pe)) {
            position = pe.Beh.GlobalPosition();
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
        typeof(Vector2), typeof(Vector2).MakeByRefType()
    });
    public static readonly ExFunction findNearestSave = ExUtils.Wrap<Enemy>("FindNearestSave", new[] {
        typeof(Vector2), typeof(int?), typeof(int).MakeByRefType(), typeof(Vector2).MakeByRefType()
    });
}
}
