using System;
using System.Collections.Generic;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using CommunityToolkit.HighPerformance;
using Danmokou.Behavior.Display;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Descriptors;
using Danmokou.Danmaku.Options;
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
using UnityEngine.Profiling;
// ReSharper disable AssignmentInConditionalExpression

namespace Danmokou.Behavior {
public class Enemy : RegularUpdater, IBehaviorEntityDependent, 
    ICircularPlayerSimpleBulletCollisionReceiver, 
    ICircularPlayerPatherCollisionReceiver,
    ICircularPlayerLaserCollisionReceiver,
    ICircularPlayerBulletCollisionReceiver {
    private const float cardCircleTrailMultiplier = 5f;
    private const float cardCircleTrailCatchup = 0.03f;
    private const float spellCircleTrailMultiplier = 30f;
    private const float spellCircleTrailCatchup = 0.02f;
    public readonly struct FrozenCollisionInfo {
        public readonly Vector2 location;
        public readonly float radius;
        public readonly int enemyIndex;
        public bool Active => allEnemies.ContainsKey(enemyIndex);
        public readonly Enemy enemy;

        public FrozenCollisionInfo(Enemy e) {
            location = e.Beh.GlobalPosition();
            radius = e.collisionRadius;
            enemy = e;
            enemyIndex = e.enemyIndex;
        }
    }

    public BehaviorEntity Beh { get; private set; } = null!;
    public Vector2 Location => Beh.Location;
    public bool takesBossDamage;
    private Maybe<Enemy> divertHP = Maybe<Enemy>.None;
    public (BulletManager.StyleSelector sel, bool exclude)? VulnerableStyles { get; private set; }
    public BPY? ReceivedDamageMult { get; set; }
    public double HP { get; private set; }
    public int maxHP = 1000;
    public int PhotoHP { get; private set; } = 1;
    private int maxPhotoHP = 1;
    public int PhotosTaken { get; private set; } = 0;
    //public bool Vulnerable { get; private set; }= true;

    private Vulnerability _vulnerable;
    public Vulnerability Vulnerable(bool bypassDamageMult = false) => 
        (!bypassDamageMult && receivedDamageMult <= 0) 
        ? Vulnerability.PASS_THROUGH : 
        _vulnerable;
    //Updated every frame based on Vulnerability
    private bool receivesBulletCollisions;
    private double receivedDamageMult;

    public bool ReceivesBulletCollisions(string? style) =>
        receivesBulletCollisions && (style is null || VulnerableStyles is not { } coll ||
                                     coll.sel.Matches(style) != coll.exclude);
    
    //private static int enemyIndexCtr = 0;
    //private int enemyIndex;

    /// <summary>
    /// The radius of this entity's hitbox when it collides with the player.
    /// </summary>
    public RFloat? taiAtariRadius = null!;
    
    /// <summary>
    /// Hurtbox radius.
    /// </summary>
    public RFloat collisionRadius = null!;
    public float CollisionRadius => collisionRadius;
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
    private bool hasCardCircle;
    public SpriteRenderer? spellCircle;
    private bool hasSpellCircle;
    public ntw.CurvedTextMeshPro.TextProOnACircle? spellCircleText;
    private Transform? cardtr;
    private Transform? spellTr;
    public SpriteRenderer? distorter;
    private bool hasDistorter;
    private MaterialPropertyBlock distortPB = null!;
    private MaterialPropertyBlock scPB = null!;
    private float healthbarStart; // 0-1
    private float healthbarSize; //As fraction of total bar, 0-1

    [ReflectInto(typeof(BPY))]
    public string healthbarOpacity = "1";
    public BPY HealthbarOpacityFunc { get; set; } = null!;

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
    private Maybe<PlayerController> target;

    private static short renderCounter = short.MinValue;
    private const short renderCounterStep = 4;

    private static short NextRenderCounter() {
        if ((renderCounter += renderCounterStep) > -renderCounterStep) {
            renderCounter = short.MinValue;
        }
        return renderCounter;
    }
    
    public ItemDrops AutoDeathItems => new(
        Math.Max(1, maxHP / 300.0), 
        maxHP >= 400 ? Mathf.CeilToInt(maxHP / 900f) : 0, 
        maxHP >= 300 ? Mathf.CeilToInt(maxHP / 800f) : 0,
        maxHP >= 400 ? Mathf.CeilToInt(maxHP / 700f) : 0,
        Mathf.CeilToInt(maxHP / 600f)
        );


    private static int enemyIndexCtr = 0;
    private int enemyIndex;
    private static readonly Dictionary<int, Enemy> allEnemies = new();
    private static readonly DMCompactingArray<Enemy> orderedEnemies = new();
    private static readonly List<FrozenCollisionInfo> frozenEnemies = new();
    public static IReadOnlyList<FrozenCollisionInfo> FrozenEnemies => frozenEnemies;

    public void LinkAndReset(BehaviorEntity _beh) {
        Beh = _beh;
        Beh.LinkDependentUpdater(this);
        var sortOrder = NextRenderCounter();
        _beh.displayer!.SetSortingOrder(sortOrder);
        if (hasSpellCircle = (spellCircle != null)) {
            spellCircle!.sortingOrder = sortOrder;
            if (spellCircleText != null)
                spellCircleText.GetComponent<TextMeshPro>().sortingOrder = sortOrder + 1;
        }
        if (hasCardCircle = (cardCircle != null))
            cardCircle!.sortingOrder = sortOrder + 2;
        if (healthbarSprite != null) healthbarSprite.sortingOrder = sortOrder + 3;
        if (hasDistorter = (distorter != null)) 
            distorter!.sortingOrder = sortOrder;
        allEnemies[enemyIndex = enemyIndexCtr++] = this;
        tokens.Add(orderedEnemies.Add(this));
        HP = maxHP;
        queuedDamage = 0;
        _vulnerable = Vulnerability.VULNERABLE;
        receivesBulletCollisions = false;
        receivedDamageMult = 0;
        target = ServiceLocator.MaybeFind<PlayerController>();
        hpPB = new MaterialPropertyBlock();
        distortPB = new MaterialPropertyBlock();
        scPB = new MaterialPropertyBlock();
        if (cameraCrosshair != null) cameraCrosshair.enabled = false;
        if (healthbarSprite != null) {
            healthbarSprite.enabled = true;
            healthbarSprite.GetPropertyBlock(hpPB);
            HealthbarOpacityFunc = ReflWrap<BPY>.Wrap(healthbarOpacity);
            hpPB.SetFloat(PropConsts.alpha, HealthbarOpacityFunc(Beh.BPI));
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

    public void Alive() {
        EnableUpdates();
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService<IPlayerSimpleBulletCollisionReceiver>(this);
        RegisterService<IPlayerPatherCollisionReceiver>(this);
        RegisterService<IPlayerLaserCollisionReceiver>(this);
        RegisterService<IPlayerBulletCollisionReceiver>(this);
    }

    public void Initialized(RealizedBehOptions? options) {
        VulnerableStyles = null;
        ReceivedDamageMult = null;
        if (options.Try(out var o)) {
            if (o.hp.Try(out var hp)) SetHP(hp, hp);
            VulnerableStyles = o.vulnerable;
            ReceivedDamageMult = o.receivedDamage;
        }
    }
    
    public void Died() {
        if (healthbarSprite != null) healthbarSprite.enabled = false;
        allEnemies.Remove(enemyIndex);
        DisableUpdates();
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
                return M.Lerp(baseRad, startRad * spellBreather(Beh.rBPI.t), (t - baseT) / SpellCircleLerpTime);
            }
            float pt = t - baseT - SpellCircleLerpTime;
            return Math.Max(MinSCScale,
                M.Lerp(startRad * spellBreather(Beh.rBPI.t), MinSCScale, pt / timeout));
        };
        RecheckGraphicsSettings();
    }

    public void DivertHP(Enemy to) {
        divertHP = to == this ? Maybe<Enemy>.None : to;
    }

    private float HPRatio => (float)(HP / maxHP);
    private float PhotoRatio => (float) PhotoHP / maxPhotoHP;
    private float BarRatio => Math.Min(PhotoRatio, HPRatio);
    [UsedImplicitly]
    public float EffectiveBarRatio => divertHP.Try(out var d) ? d.EffectiveBarRatio : BarRatio;
    private float _displayBarRatio;
    public float DisplayBarRatio => divertHP.Try(out var d) ? d.DisplayBarRatio : _displayBarRatio;

    private Color HPColor => currPhaseType == PhaseType.Timeout ?
            unfilledColor :
            //Approximation to make the max color appear earlier
            Color.Lerp(currPhase.color2, currPhase.color1, Mathf.Pow(_displayBarRatio, 1.5f));
    public Color UIHPColor => (currPhaseType == PhaseType.Timeout || currPhaseType == PhaseType.Dialogue || currPhaseType == null) ? 
        Color.clear : HPColor;

    public override void RegularUpdate() {
        receivedDamageMult = ReceivedDamageMult?.Invoke(Beh.rBPI) ?? 1;
        receivesBulletCollisions =
            LocationHelpers.OnPlayableScreenBy(-0.3f, Beh.GlobalPosition()) && Vulnerable().HitsLand();
        _displayBarRatio = M.Lerp(_displayBarRatio, BarRatio, HPLerpRate * ETime.FRAME_TIME);
        if (hasDistorter) {
            distortPB.SetFloat(PropConsts.time, Beh.rBPI.t);
            distorter!.SetPropertyBlock(distortPB);
        }
        if (hasCardCircle) {
            var rt = cardtr!.localEulerAngles;
            rt += ETime.FRAME_TIME * cardRotator(Beh.rBPI);
            cardtr.localEulerAngles = rt;
            var scale = cardBreather(Beh.rBPI.t);
            cardtr.localScale = new Vector3(scale, scale, scale);
            cardtr.localPosition = Vector2.Lerp(cardtr.localPosition, -Beh.LastDelta * cardCircleTrailMultiplier, cardCircleTrailCatchup);
        }
        if (hasSpellCircle) {
            var rt = spellTr!.localEulerAngles;
            rt += ETime.FRAME_TIME * spellRotator(Beh.rBPI);
            spellTr.localEulerAngles = rt;
            scPB.SetFloat(PropConsts.time, Beh.rBPI.t);
            if (spellCircleCancel.Cancelled) {
                float baseT = Beh.rBPI.t;
                float currRad = lastSpellCircleRad;
                spellCircleRad = t => M.Lerp(currRad, LerpFromSCScale, (t - baseT) / SpellCircleLerpTime);
                spellCircleCancel = Cancellable.Null;
            }
            lastSpellCircleRad = spellCircleRad?.Invoke(Beh.rBPI.t) ?? lastSpellCircleRad;
            if (ReferenceEquals(spellCircleCancel, Cancellable.Null) && lastSpellCircleRad <= LerpFromSCScale) {
                spellCircle!.gameObject.SetActive(false);
            }
            spellTr.localScale = new Vector3(lastSpellCircleRad, lastSpellCircleRad, lastSpellCircleRad);
            spellTr.localPosition = Vector2.Lerp(spellTr.localPosition, -Beh.LastDelta * spellCircleTrailMultiplier, spellCircleTrailCatchup);
            //scPB.SetFloat(PropConsts.radius, lastSpellCircleRad);
            spellCircle!.SetPropertyBlock(scPB);
            //if (spellCircleText != null) spellCircleText.SetRadius(lastSpellCircleRad + SpellCircleTextRadOffset);
        }
        for (int ii = 0; ii < hitCooldowns.Keys.Count; ++ii) {
            if (hitCooldowns.Keys.GetMarkerIfExistsAt(ii, out var dm)) {
                if (--hitCooldowns[dm.Value] <= 0)
                    dm.MarkForDeletion();
            }
        }
        hitCooldowns.Keys.Compact();
    }

    public override void RegularUpdateCollision() {
        if (taiAtariRadius != null && taiAtariRadius > 0 && target.Try(out var player)) {
            var hb = player.Hurtbox;
            if (CollisionMath.CircleOnCircle(Location, taiAtariRadius, in hb.x, in hb.y, in hb.radius))
                player.TakeHit();
        }
    }

    public override void RegularUpdateFinalize() {
        base.RegularUpdateFinalize();
        PollDamage();
        PollPhotoDamage();
        if (--dmgLabelBuffer == 0 && labelAccDmg > 0) {
            Beh.DropDropLabel(dmgGrad, $"{labelAccDmg:n0}");
            labelAccDmg = 0;
        }
        if (healthbarSprite != null) {
            hpPB.SetFloat(PropConsts.alpha, HealthbarOpacityFunc(Beh.BPI));
            hpPB.SetFloat(PropConsts.fillRatio, DisplayBarRatio);
            hpPB.SetColor(PropConsts.fillColor, HPColor);
            hpPB.SetFloat(PropConsts.time, Beh.rBPI.t);
            healthbarSprite.SetPropertyBlock(hpPB);
        }
    }
    
    public static void FreezeEnemies() {
        frozenEnemies.Clear();
        orderedEnemies.Compact();
        for (int ii = 0; ii < orderedEnemies.Count; ++ii) {
            var enemy = orderedEnemies[ii];
            if (LocationHelpers.OnPlayableScreen(enemy.Beh.GlobalPosition()) && enemy.Vulnerable().HitsLand()) {
                frozenEnemies.Add(new FrozenCollisionInfo(enemy));
            }
        }
    }

    #region ReceiveCollisionsFromPlayerBullets
    
    public bool TakeHit(in PlayerBullet pb, in Vector2 location, in uint bpiId) {
        if ((pb.data.bossDmg > 0 || pb.data.stageDmg > 0)
            && (pb.data.destructible || TryHitIndestructible(bpiId, pb.data.cdFrames))) {
            QueuePlayerDamage(pb.data.bossDmg, pb.data.stageDmg, pb.firer);
            ProcOnHit(pb.data.effect, location);
            return true;
        } else
            return false;
    }
    
    bool ICircularPlayerSimpleBulletCollisionReceiver.TakeHit(in PlayerBullet pb, in ParametricInfo bpi) =>
        TakeHit(in pb, bpi.loc, in bpi.id);
    void ICircularPlayerPatherCollisionReceiver.TakeHit(CurvedTileRenderPather pather, Vector2 collLoc,
        PlayerBullet plb) => TakeHit(plb, collLoc, pather.BPI.id);
    void ICircularPlayerLaserCollisionReceiver.TakeHit(CurvedTileRenderLaser laser, Vector2 collLoc,
        PlayerBullet plb) => TakeHit(plb, collLoc, laser.BPI.id);
    void ICircularPlayerBulletCollisionReceiver.TakeHit(Bullet bullet, Vector2 collLoc,
        PlayerBullet plb) => TakeHit(plb, collLoc, bullet.BPI.id);
    

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

    private double QueuePlayerDamage(double dmg, PlayerController firer, bool bypassDamageMult = false, bool addToLabel = true) {
        if (!Vulnerable(bypassDamageMult).TakesDamage()) return 0;
        if (!bypassDamageMult)
            dmg *= receivedDamageMult;
        if (dmg <= 0)
            return 0;
        if (divertHP.Try(out var divert)) {
            var divertedDmg = divert.QueuePlayerDamage(dmg, firer, true, false);
            if (addToLabel)
                AddDmgToLabel(divertedDmg);
            return divertedDmg;
        }
        float dstToFirer = (firer.Location - Beh.rBPI.LocV2).magnitude;
        float shotgun = (SHOTGUN_MIN - dstToFirer) / (SHOTGUN_MIN - SHOTGUN_MAX);
        double multiplier = GameManagement.Instance.PlayerDamageMultiplier *
                            M.Lerp(0, 1, shotgun, 1, SHOTGUN_MULTIPLIER);
        dmg *= multiplier * GameManagement.Difficulty.playerDamageMod;
        queuedDamage += dmg;
        if (addToLabel)
            AddDmgToLabel(queuedDamage);
        Counter.DoShotgun(shotgun);
        return queuedDamage;
    }

    private void AddDmgToLabel(double dmg) {
        labelAccDmg += (long)dmg;
        if (dmgLabelBuffer <= 0) dmgLabelBuffer = DMG_LABEL_BUFFER;
    }
    
    private void PollDamage() {
        if (!Vulnerable(true).TakesDamage()) queuedDamage = 0;
        if (queuedDamage < 1) return;
        HP = M.Clamp(0f, maxHP, HP - queuedDamage);
        queuedDamage = 0;
        if (HP <= 0) {
            Beh.OutOfHP();
            _vulnerable = Vulnerability.NO_DAMAGE; //Wait for new hp value to be declared
        } else if (modifyDamageSound) {
            if ((float) HP / maxHP < LOW_HP_THRESHOLD) {
                Counter.AlertLowEnemyHP();
            }
        }
    }
    
    #endregion
    
    
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
            _vulnerable = Vulnerability.NO_DAMAGE;
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

    public void SetVulnerable(Vulnerability v) => _vulnerable = v;

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

    //Note that we should not use a dictionary from ID to frame number, since hit cooldowns freeze with the enemy
    private readonly DictionaryWithKeys<uint, int> hitCooldowns = new();
    public bool TryHitIndestructible(uint id, int cooldownFrames) {
        if (hitCooldowns.Data.ContainsKey(id))
            return false;
        hitCooldowns[id] = cooldownFrames;
        return true;
    }

    public void ProcOnHit(EffectStrategy effect, Vector2 hitLoc) => effect.Proc(hitLoc, Beh.GlobalPosition(), collisionRadius);

    private bool ViewfinderHits(CRect viewfinder) => 
        Vulnerable().TakesDamage() && ayaCameraRadius != null && 
        CollisionMath.CircleInRect(Beh.rBPI.loc.x, Beh.rBPI.loc.y, ayaCameraRadius, viewfinder);
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

    private static readonly VTP SuicideVTP = CSVTPRepo.RVelocity(_ => new Vector2(1.6f, 0));
    public void DoSuicideFire() {
        var bt = LevelController.DefaultSuicideStyle;
        if (string.IsNullOrWhiteSpace(bt)) 
            bt = "triangle-black/w";
        var angleTo = M.AtanD(ServiceLocator.Find<PlayerController>().Location - Beh.rBPI.LocV2);
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
        if (taiAtariRadius != null) Handles.DrawWireDisc(position, Vector3.forward, taiAtariRadius);
        Handles.color = Color.green;
        Handles.DrawWireDisc(position, Vector3.forward, 0.9f);
            Handles.color = Color.cyan;
            if (collisionRadius > 0) 
                Handles.DrawWireDisc(position, Vector3.forward, collisionRadius);
    }
#endif
    
    [UsedImplicitly]
    public static bool FindNearest(Vector3 source, out Vector2 position) {
        Profiler.BeginSample("FindNearest");
        bool found = false;
        position = default;
        float lastDist = 0f;
        for (int ie = 0; ie < frozenEnemies.Count; ++ie) {
            var e = frozenEnemies[ie];
            if (e.Active && LocationHelpers.OnPlayableScreen(e.location)) {
                var dst = (e.location.x - source.x) * (e.location.x - source.x) + (e.location.y - source.y) * (e.location.y - source.y);
                if (!found || dst < lastDist) {
                    lastDist = dst;
                    found = true;
                    position = e.location;
                }
            }
        }
        Profiler.EndSample();
        return found;
    }

    [UsedImplicitly]
    public static bool FindNearestSave(Vector3 source, int? preferredEnemy, out int enemy, out Vector2 position) {
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
            if (e.Active && LocationHelpers.OnPlayableScreen(e.location)) {
                var dst = (e.location.x - source.x) * (e.location.x - source.x) + (e.location.y - source.y) * (e.location.y - source.y);
                if (!found || dst < lastDist) {
                    lastDist = dst;
                    found = true;
                    enemy = e.enemyIndex;
                    position = e.location;
                }
            }
        }
        return found;
    }

    public static readonly ExFunction findNearest = ExFunction.Wrap<Enemy>("FindNearest", new[] {
        typeof(Vector3), typeof(Vector2).MakeByRefType()
    });
    public static readonly ExFunction findNearestSave = ExFunction.Wrap<Enemy>("FindNearestSave", new[] {
        typeof(Vector3), typeof(int?), typeof(int).MakeByRefType(), typeof(Vector2).MakeByRefType()
    });
    
    
}
}
