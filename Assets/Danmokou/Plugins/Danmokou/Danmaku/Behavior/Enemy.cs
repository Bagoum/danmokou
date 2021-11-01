using System;
using System.Collections.Generic;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Expressions;
using Danmokou.Behavior.Display;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Expressions;
using Danmokou.Graphics;
using Danmokou.Player;
using Danmokou.Reflection;
using JetBrains.Annotations;
using TMPro;
using UnityEditor;
using UnityEngine;
using Danmokou.Scriptables;
using Danmokou.Services;

namespace Danmokou.Behavior {
public class Enemy : RegularUpdater {
    private const float cardCircleTrailMultiplier = 5f;
    private const float cardCircleTrailCatchup = 0.03f;
    private const float spellCircleTrailMultiplier = 30f;
    private const float spellCircleTrailCatchup = 0.02f;
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

    public BehaviorEntity Beh { get; private set; } = null!;
    public bool takesBossDamage;
    private (bool _, Enemy to)? divertHP = null;
    public double HP { get; private set; }
    public int maxHP = 1000;
    public int PhotoHP { get; private set; } = 1;
    private int maxPhotoHP = 1;
    public int PhotosTaken { get; private set; } = 0;
    //public bool Vulnerable { get; private set; }= true;

    public Vulnerability Vulnerable { get; private set; }
    //private static int enemyIndexCtr = 0;
    //private int enemyIndex;

    public RFloat collisionRadius = null!;
    /// <summary>
    /// The entirety of this circle must be within the viewfinder for a capture to succeed.
    /// </summary>
    public RFloat? ayaCameraRadius;
    

    private const float LOW_HP_THRESHOLD = .2f;

    public bool modifyDamageSound;

    public SpriteRenderer? cameraCrosshair;

    [Header("Healthbar Controller (Optional)")]
    public SpriteRenderer? healthbarSprite;
    private MaterialPropertyBlock hpPB = null!;
    public SpriteRenderer? cardCircle;
    public SpriteRenderer? spellCircle;
    public ntw.CurvedTextMeshPro.TextProOnACircle? spellCircleText;
    private Transform? cardtr;
    private Transform? spellTr;
    public SpriteRenderer? distorter;
    private MaterialPropertyBlock distortPB = null!;
    private MaterialPropertyBlock scPB = null!;
    private float healthbarStart; // 0-1
    private float healthbarSize; //As fraction of total bar, 0-1

    public RColor2 nonspellColor = null!;
    public RColor2 spellColor = null!;

    public RColor unfilledColor = null!;

    public RFloat hpRadius = null!;
    public RFloat hpThickness = null!;

    //Previously 6f, increasing for fire shader
    private const float HPLerpRate = 14f;

    private FXY cardBreather = t => 1f + M.Sine(7, 0.1f, t);
    private TP3 cardRotator = _ => new Vector3(0, 0, 60);
    private FXY spellBreather = t => 1f + M.Sine(2.674f, 0.1f, t);
    private TP3 spellRotator = _ => Vector3.zero;

    private static short renderCounter = short.MinValue;
    private const short renderCounterStep = 4;

    private static short NextRenderCounter() {
        if ((renderCounter += renderCounterStep) > -renderCounterStep) {
            renderCounter = short.MinValue;
        }
        return renderCounter;
    }
    
    public ItemDrops AutoDeathItems => new ItemDrops(
        Math.Max(1, maxHP / 300.0), 
        maxHP >= 400 ? Mathf.CeilToInt(maxHP / 900f) : 0, 
        maxHP >= 300 ? Mathf.CeilToInt(maxHP / 800f) : 0,
        maxHP >= 400 ? Mathf.CeilToInt(maxHP / 700f) : 0,
        Mathf.CeilToInt(maxHP / 600f)
        );


    private static int enemyIndexCtr = 0;
    private int enemyIndex;
    private static readonly Dictionary<int, Enemy> allEnemies = new Dictionary<int, Enemy>();
    private static readonly DMCompactingArray<Enemy> orderedEnemies = new DMCompactingArray<Enemy>();
    private DeletionMarker<Enemy> aliveToken = null!;
    private static readonly List<FrozenCollisionInfo> frozenEnemies = new List<FrozenCollisionInfo>();
    public static IReadOnlyList<FrozenCollisionInfo> FrozenEnemies => frozenEnemies;

    public void Initialize(BehaviorEntity _beh) {
        Beh = _beh;
        var sortOrder = NextRenderCounter();
        _beh.displayer!.SetSortingOrder(sortOrder);
        if (spellCircle != null) {
            spellCircle.sortingOrder = sortOrder;
            if (spellCircleText != null)
                spellCircleText.GetComponent<TextMeshPro>().sortingOrder = sortOrder + 1;
        }
        if (cardCircle != null) cardCircle.sortingOrder = sortOrder + 2;
        if (healthbarSprite != null) healthbarSprite.sortingOrder = sortOrder + 3;
        if (distorter != null) distorter.sortingOrder = sortOrder;
        allEnemies[enemyIndex = enemyIndexCtr++] = this;
        aliveToken = orderedEnemies.Add(this);
        HP = maxHP;
        queuedDamage = 0;
        Vulnerable = Vulnerability.VULNERABLE;
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
            SetHPBarColors(null);
            hpPB.SetColor(PropConsts.unfillColor, unfilledColor);
            _displayBarRatio = BarRatio;
            hpPB.SetFloat(PropConsts.fillRatio, DisplayBarRatio);
            hpPB.SetColor(PropConsts.fillColor, currPhase.color1);
            healthbarSprite.SetPropertyBlock(hpPB);
        }
        if (cardCircle != null) {
            cardtr = cardCircle.transform;
            cardCircle.enabled = false;
        }
        if (distorter != null) {
            distorter.material = Instantiate(distorter.material);
            distorter.GetPropertyBlock(distortPB);
            distortPB.SetFloat(PropConsts.time, 0f);
            distorter.SetPropertyBlock(distortPB);
            distorter.enabled = false;
        }
        if (spellCircle != null) {
            spellTr = spellCircle.transform;
            spellCircle.GetPropertyBlock(scPB);
            scPB.SetFloat(PropConsts.time, 0f);
            spellCircle.SetPropertyBlock(scPB);
            spellCircle.gameObject.SetActive(false);
        }
        lastSpellCircleRad = LerpFromSCScale;
    }
    
    public void ConfigureBoss(BossConfig b) {
        if (b.colors.cardColorR.a > 0 || b.colors.cardColorG.a > 0 || b.colors.cardColorB.a > 0) {
            RequestCardCircle(b.colors.cardColorR, b.colors.cardColorG, b.colors.cardColorB, b.CardRotator);
        }
        spellRotator = b.SpellRotator;
        SetSpellCircleColors(b.colors.spellColor1, b.colors.spellColor2, b.colors.spellColor3);
        if (cameraCrosshair != null) cameraCrosshair.color = b.colors.uiHPColor;
    }

    public void DisableDistortion() {
        if (distorter != null)
            distorter.material.EnableKeyword("SHADOW_ONLY");
    }
    private void RequestCardCircle(Color colorR, Color colorG, Color colorB, TP3 rotator) {
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
        scPB.SetColor(PropConsts.redColor, c1);
        scPB.SetColor(PropConsts.greenColor, c2);
        scPB.SetColor(PropConsts.blueColor, c3);
    }

    private void RecheckGraphicsSettings() {
        if (cardCircle != null && distorter != null) {
            distorter.enabled = cardCircle.enabled & SaveData.s.Shaders;
        }
    }

    private ICancellee spellCircleCancel = Cancellable.Null;
    private FXY? spellCircleRad;
    private const float MinSCScale = 1f;
    private const float LerpFromSCScale = 0.1f;
    private float lastSpellCircleRad;
    private const float SpellCircleLerpTime = 0.6f;
    private const float SMulpellCircleTextRadOffset = 0.08f;
    public void RequestSpellCircle(float timeout, ICancellee cT, float startRad=1.7f) {
        if (timeout < 0.1) timeout = M.IntFloatMax;
        if (spellCircle == null) return;
        spellCircleCancel = cT;
        spellCircle.gameObject.SetActive(true);
        float baseT = Beh.rBPI.t;
        float baseRad = Math.Max(LerpFromSCScale, lastSpellCircleRad);
        spellCircleRad = t => {
            if (t < baseT + SpellCircleLerpTime) {
                return Mathf.Lerp(baseRad, startRad * spellBreather(Beh.rBPI.t), (t - baseT) / SpellCircleLerpTime);
            }
            float pt = t - baseT - SpellCircleLerpTime;
            return Mathf.Max(MinSCScale,
                Mathf.Lerp(startRad * spellBreather(Beh.rBPI.t), MinSCScale, pt / timeout));
        };
        RecheckGraphicsSettings();
    }

    public void DivertHP(Enemy to) => divertHP = (false, to);
    private float HPRatio => (float)(HP / maxHP);
    private float PhotoRatio => (float) PhotoHP / maxPhotoHP;
    private float BarRatio => Math.Min(PhotoRatio, HPRatio);
    [UsedImplicitly]
    public float EffectiveBarRatio => divertHP?.to.EffectiveBarRatio ?? BarRatio;
    private float _displayBarRatio;
    public float DisplayBarRatio => divertHP?.to.DisplayBarRatio ?? _displayBarRatio;

    private Color HPColor => currPhaseType == PhaseType.TIMEOUT ?
            unfilledColor :
            //Approximation to make the max color appear earlier
            Color.Lerp(currPhase.color2, currPhase.color1, Mathf.Pow(_displayBarRatio, 1.5f));
    public Color UIHPColor => (currPhaseType == PhaseType.TIMEOUT || currPhaseType == PhaseType.DIALOGUE || currPhaseType == null) ? 
        Color.clear : HPColor;

    public override void RegularUpdate() {
        PollDamage();
        PollPhotoDamage();
        if (--dmgLabelBuffer == 0 && labelAccDmg > 0) {
            Beh.DropDropLabel(dmgGrad, $"{labelAccDmg:n0}");
            labelAccDmg = 0;
        }
        _displayBarRatio = Mathf.Lerp(_displayBarRatio, BarRatio, HPLerpRate * ETime.FRAME_TIME);
        if (healthbarSprite != null) {
            hpPB.SetFloat(PropConsts.fillRatio, DisplayBarRatio);
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
            var rt = cardtr!.localEulerAngles;
            rt += ETime.FRAME_TIME * cardRotator(Beh.rBPI);
            cardtr.localEulerAngles = rt;
            var scale = cardBreather(Beh.rBPI.t);
            cardtr.localScale = new Vector3(scale, scale, scale);
            cardtr.localPosition = Vector2.Lerp(cardtr.localPosition, -Beh.LastDelta * cardCircleTrailMultiplier, cardCircleTrailCatchup);
        }
        if (spellCircle != null) {
            var rt = spellTr!.localEulerAngles;
            rt += ETime.FRAME_TIME * spellRotator(Beh.rBPI);
            spellTr.localEulerAngles = rt;
            scPB.SetFloat(PropConsts.time, Beh.rBPI.t);
            if (spellCircleCancel.Cancelled) {
                float baseT = Beh.rBPI.t;
                float currRad = lastSpellCircleRad;
                spellCircleRad = t => Mathf.Lerp(currRad, LerpFromSCScale, (t - baseT) / SpellCircleLerpTime);
                spellCircleCancel = Cancellable.Null;
            }
            lastSpellCircleRad = spellCircleRad?.Invoke(Beh.rBPI.t) ?? lastSpellCircleRad;
            if (ReferenceEquals(spellCircleCancel, Cancellable.Null) && lastSpellCircleRad <= LerpFromSCScale) {
                spellCircle.gameObject.SetActive(false);
            }
            spellTr.localScale = new Vector3(lastSpellCircleRad, lastSpellCircleRad, lastSpellCircleRad);
            spellTr.localPosition = Vector2.Lerp(spellTr.localPosition, -Beh.LastDelta * spellCircleTrailMultiplier, spellCircleTrailCatchup);
            //scPB.SetFloat(PropConsts.radius, lastSpellCircleRad);
            spellCircle.SetPropertyBlock(scPB);
            //if (spellCircleText != null) spellCircleText.SetRadius(lastSpellCircleRad + SpellCircleTextRadOffset);
        }
        for (int ii = 0; ii < hitCooldowns.Count; ++ii) {
            if (hitCooldowns[ii].cooldown <= 1) hitCooldowns.Delete(ii);
            else hitCooldowns[ii].cooldown -= 1;
        }
        hitCooldowns.Compact();
    }

    public static void FreezeEnemies() {
        frozenEnemies.Clear();
        orderedEnemies.Compact();
        for (int ii = 0; ii < orderedEnemies.Count; ++ii) {
            var enemy = orderedEnemies[ii];
            if (LocationHelpers.OnPlayableScreen(enemy.Beh.GlobalPosition()) && enemy.Vulnerable.HitsLand()) {
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

    private double queuedDamage = 0;
    private int queuedPhotoDamage = 0;

    //The reason we queue damage is to avoid calling eg. SM clear effects while in the middle of other entities' update loops.
    public void QueuePlayerDamage(int bossDmg, int stageDmg, PlayerController firer) => 
        QueuePlayerDamage(takesBossDamage ? bossDmg : stageDmg, firer);

    private void QueuePlayerDamage(int dmg, PlayerController firer) {
        if (divertHP != null) {
            divertHP.Value.to.QueuePlayerDamage(dmg, firer);
            return;
        }
        if (!Vulnerable.TakesDamage()) return;
        float dstToFirer = (firer.Loc - Beh.rBPI.loc).magnitude;
        float shotgun = (SHOTGUN_MIN - dstToFirer) / (SHOTGUN_MIN - SHOTGUN_MAX);
        double multiplier = GameManagement.Instance.PlayerDamageMultiplier *
                            M.Lerp(0, 1, shotgun, 1, SHOTGUN_MULTIPLIER);
        queuedDamage += dmg * multiplier * GameManagement.Difficulty.playerDamageMod;
        Counter.DoShotgun(shotgun);
    }
    private void PollDamage() {
        if (!Vulnerable.TakesDamage()) queuedDamage = 0;
        if (queuedDamage < 1) return;
        HP = M.Clamp(0f, maxHP, HP - queuedDamage);
        labelAccDmg += (long)queuedDamage;
        if (dmgLabelBuffer <= 0) dmgLabelBuffer = DMG_LABEL_BUFFER;
        queuedDamage = 0;
        if (HP <= 0) {
            Beh.OutOfHP();
            Vulnerable = Vulnerability.NO_DAMAGE; //Wait for new hp value to be declared
        } else if (modifyDamageSound) {
            if ((float) HP / maxHP < LOW_HP_THRESHOLD) {
                Counter.AlertLowEnemyHP();
            }
        }
    }
    
    
    private long labelAccDmg = 0;
    private int dmgLabelBuffer = -1;
    private const int DMG_LABEL_BUFFER = 30;
    
    private static readonly IGradient dmgGrad = ColorHelpers.FromKeys(new[] {
        new GradientColorKey(new Color32(255, 10, 138, 255), 0.1f), 
        new GradientColorKey(new Color32(240, 0, 52, 255), 0.8f), 
    }, DropLabel.defaultAlpha);

    private void PollPhotoDamage() {
        if (queuedPhotoDamage < 1) return;
        PhotosTaken += queuedPhotoDamage;
        PhotoHP = M.Clamp(0, maxPhotoHP, PhotoHP - queuedPhotoDamage);
        queuedPhotoDamage = 0;
        if (PhotoHP == 0) {
            Beh.OutOfHP();
            Vulnerable = Vulnerability.NO_DAMAGE;
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

    public void SetVulnerable(Vulnerability v) => Vulnerable = v;

    private Color2 currPhase;
    private PhaseType? currPhaseType = null;
    private Color2 CardToColor(PhaseType? st) {
        return (st?.IsSpell() ?? false) ? spellColor : nonspellColor;
    }
    private Color2 CardToColorInv(PhaseType? st) {
        return (st?.IsSpell() ?? false) ? nonspellColor : spellColor;
    }
    public void SetHPBarColors(PhaseType? st) {
        currPhaseType = st;
        currPhase = CardToColor(st);
        hpPB.SetColor(PropConsts.R2NColor, CardToColorInv(st).color1);
        hpPB.SetFloat(PropConsts.R2CPhaseStart, healthbarStart + healthbarSize);
        hpPB.SetFloat(PropConsts.R2NPhaseStart, healthbarStart);
    }

    public IReadOnlyList<Enemy>? Subbosses { get; set; } = null;
    public void SetHPBar(float? portion, PhaseType? color) {
        if (portion.Try(out var _portion)) {
            if (healthbarStart < 0.1f || (color?.RequiresFullHPBar() ?? false)) {
                healthbarStart = 1f;
            } else {
                _displayBarRatio = 1f + _displayBarRatio * healthbarSize / (healthbarStart * _portion);
            }
            healthbarSize = healthbarStart * _portion;
            healthbarStart -= healthbarSize;
        }
        SetHPBarColors(color);
        if (Subbosses != null) {
            foreach (var e in Subbosses) e.SetHPBar(portion, color);
        }
    }

    private class HitCooldown {
        public readonly uint id;
        public int cooldown;
        
        public HitCooldown(uint id, int cooldown) {
            this.id = id;
            this.cooldown = cooldown;
        }
    }
    //"Slower" than using a dictionary, but there are few enough colliding persistent objects at a time that 
    //it's better to optimize for garbage. 
    private readonly DMCompactingArray<HitCooldown> hitCooldowns = new DMCompactingArray<HitCooldown>(8);
    public bool TryHitIndestructible(uint id, int cooldownFrames) {
        for (int ii = 0; ii < hitCooldowns.Count; ++ii) {
            if (hitCooldowns[ii].id == id) return false;
        }
        hitCooldowns.Add(new HitCooldown(id, cooldownFrames));
        return true;
    }

    public void ProcOnHit(EffectStrategy effect, Vector2 hitLoc) => effect.Proc(hitLoc, Beh.GlobalPosition(), collisionRadius);

    private bool ViewfinderHits(CRect viewfinder) => 
        Vulnerable.TakesDamage() && ayaCameraRadius != null && 
        CollisionMath.CircleInRect(Beh.rBPI.loc, ayaCameraRadius, viewfinder);
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
        if (ViewfinderHits(viewfinder)) {
            queuedPhotoDamage += 1;
            return true;
        } else return false;
    }

    private static readonly VTP SuicideVTP = VTPRepo.RVelocity(_ => new Vector2(1.6f, 0));
    public void DoSuicideFire() {
        var bt = LevelController.DefaultSuicideStyle;
        if (string.IsNullOrWhiteSpace(bt)) 
            bt = "triangle-black/w";
        var angleTo = M.AtanD(BulletManager.PlayerTarget.location - Beh.rBPI.loc);
        int numBullets = GameManagement.Difficulty.numSuicideBullets;
        for (int ii = 0; ii < numBullets; ++ii) {
            var mov = new Movement(SuicideVTP, Beh.rBPI.loc,
                angleTo + (ii - numBullets / 2) * 120f / numBullets);
            BulletManager.RequestSimple(bt!, null, null, in mov, new ParametricInfo(in mov), false);
        }
    }
    
#if UNITY_EDITOR
    private void OnDrawGizmos() {
        var position = transform.position;
        Handles.color = Color.red;
        if (ayaCameraRadius != null) Handles.DrawWireDisc(position, Vector3.forward, ayaCameraRadius);
        Handles.color = Color.green;
        Handles.DrawWireDisc(position, Vector3.forward, 0.9f);
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
            if (e.Active && LocationHelpers.OnPlayableScreen(e.pos)) {
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
            if (e.Active && LocationHelpers.OnPlayableScreen(e.pos)) {
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

    public static readonly ExFunction findNearest = ExFunction.Wrap<Enemy>("FindNearest", new[] {
        typeof(Vector2), typeof(Vector2).MakeByRefType()
    });
    public static readonly ExFunction findNearestSave = ExFunction.Wrap<Enemy>("FindNearestSave", new[] {
        typeof(Vector2), typeof(int?), typeof(int).MakeByRefType(), typeof(Vector2).MakeByRefType()
    });
}
}
