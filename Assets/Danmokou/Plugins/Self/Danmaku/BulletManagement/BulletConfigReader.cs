using System.Collections.Generic;
using UnityEngine;
using System;
using System.CodeDom;
using DMath;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine.Profiling;

namespace Danmaku {

/// <summary>
/// A megaclass that manages loading and managing all bullet configuration,
/// updating/rendering simple bullets,
/// and providing standardized interfaces for summoning all bullets.
/// </summary>
public partial class BulletManager : RegularUpdater {
    private const float PLAYER_SB_OPACITY_MUL = 0.5f;
    private const float PLAYER_SB_FADEIN_MUL = 0.4f;
    private const float PLAYER_SB_SCALEIN_MUL = 0.1f;
    private const float PLAYER_FB_OPACITY_MUL = 0.45f;
    private readonly struct CollidableInfo {
        public readonly GenericColliderInfo.ColliderType colliderType;
        // For circle approximation
        public readonly float effRadius;
        // Circle/Line
        public readonly float radius;
        // Line
        public readonly Vector2 linePt1;
        public readonly Vector2 delta;
        // Line
        public readonly float deltaMag2;
        // Rect
        public readonly Vector2 halfRect;
        // Line/Rect
        public readonly float maxDist2;

        public CollidableInfo(GenericColliderInfo cc) {
            effRadius = cc.effectiveCircleRadius;
            colliderType = cc.colliderType;
            radius = cc.radius;
            linePt1 = cc.point1;
            delta = cc.point2 - cc.point1;
            deltaMag2 = delta.sqrMagnitude;
            halfRect = new Vector2(cc.rectHalfX, cc.rectHalfY);
            if (colliderType == GenericColliderInfo.ColliderType.Rectangle) {
                maxDist2 = cc.rectHalfX * cc.rectHalfX + cc.rectHalfY * cc.rectHalfY;
            } else {
                float maxDist = Mathf.Max(cc.point1.magnitude, cc.point2.magnitude) + radius;
                maxDist2 = maxDist * maxDist;
            }

        }
    }
    private struct BulletInCode {
        public string name;
        private readonly DeferredTextureConstruction deferredRI;
        private bool riLoaded;
        private MeshGenerator.RenderInfo ri;
        public readonly SOPlayerHitbox collisionTarget;
        public readonly int damageAgainstPlayer;
        public readonly int againstEnemyCooldown;
        public readonly bool destructible;
        public readonly bool deletable;
        public readonly CollidableInfo cc;
        public readonly ushort grazeEveryFrames;
        private readonly float DEFAULT_CULL_RAD;
        public float CULL_RAD;
        public bool Recolorizable => deferredRI.recolorizable;
        public (TP4 black, TP4 white)? recolor;

        public BulletInCode Copy(string newName) {
            GetOrLoadRI();
            MeshGenerator.RenderInfo nri = new MeshGenerator.RenderInfo(ri.material, ri.mesh, true);
            var bic = this;
            bic.name = newName;
            bic.ri = nri;
            return bic;
        }
        public void SetPlayer() {
            ri.material.SetFloat(PropConsts.scaleInT, PLAYER_SB_SCALEIN_MUL * ri.material.GetFloat(PropConsts.scaleInT));
            ri.material.SetFloat(PropConsts.fadeInT, PLAYER_SB_FADEIN_MUL * ri.material.GetFloat(PropConsts.fadeInT));
            ri.material.SetFloat(PropConsts.SharedOpacityMul, PLAYER_SB_OPACITY_MUL);
        }

        public MeshGenerator.RenderInfo GetOrLoadRI() {
            if (!riLoaded) {
                ri = deferredRI.CreateDeferredTexture();
                riLoaded = true;
            }
            return ri;
        }
        public BulletInCode(string name, DeferredTextureConstruction dfc, GenericColliderInfo cc,
            SimpleBulletEmptyScript sbes) {
            this.name = name;
            deferredRI = dfc;
            ri = default;
            riLoaded = false;
            damageAgainstPlayer = sbes.damage;
            againstEnemyCooldown = sbes.framesPerHit;
            destructible = sbes.destructible;
            deletable = sbes.deletable;
            collisionTarget = main.bulletCollisionTarget;
            this.cc = new CollidableInfo(cc);
            //Minus 1 to allow for zero offset
            grazeEveryFrames = (ushort)(sbes.grazeEveryFrames - 1);
            DEFAULT_CULL_RAD = CULL_RAD = sbes.screenCullRadius;
            recolor = null;
        }

        public void ResetMetadata() {
            CULL_RAD = DEFAULT_CULL_RAD;
            recolor = null;
        }
    }
    [Serializable]
    public struct GradientVariant {
        public string name;
        public ColorMap gradient;
    }
    public Material simpleBulletMaterial;
    public SOPlayerHitbox bulletCollisionTarget;
    public static SOPlayerHitbox PlayerTarget => main.bulletCollisionTarget;
    public SOPrefabs bulletStylesList;
    public Palette[] basicGradientPalettes;
    
    /// <summary>
    /// Complex bullets (lasers, pathers).
    /// </summary>
    private static readonly Dictionary<string, DeferredFramesRecoloring> bulletStyles = new Dictionary<string, DeferredFramesRecoloring>();

    private static void AddFaBStyle(string key, DeferredFramesRecoloring dfr) {
        bulletStyles[key] = dfr;
        nonSimplePoolNames.Add(key);
    }

    private static readonly HashSet<string> nonSimplePoolNames = new HashSet<string>();
    
    /// <summary>
    /// Simple bullets. (NPC bullets, copy-pool NPC bullets, most player bullets).
    /// This collection is only updated when pools are created, or copy-pools are deleted.
    /// </summary>
    private static readonly Dictionary<string, SimpleBulletCollection> simpleBulletPools = new Dictionary<string, SimpleBulletCollection>();
    private static void AddSimpleStyle(string key, SimpleBulletCollection sbc) {
        simpleBulletPools[key] = sbc;
        simplePoolNames.Add(key);
    }

    private static void DestroySimpleStyle(string key) {
        simpleBulletPools.Remove(key);
        simplePoolNames.Remove(key);
    }
    private static readonly HashSet<string> simplePoolNames = new HashSet<string>();

    /// <summary>
    /// Currently activated bullet styles. All styles are deactivated on scene change, and
    /// activated when they are used for the first time.
    /// </summary>
    private static readonly List<SimpleBulletCollection> activeNpc = new List<SimpleBulletCollection>(350);
    private static readonly List<SimpleBulletCollection> activePlayer = new List<SimpleBulletCollection>(50);
    private static readonly List<SimpleBulletCollection> activeCEmpty = new List<SimpleBulletCollection>(8); //Require priority
    private static readonly List<SimpleBulletCollection> activeCNpc = new List<SimpleBulletCollection>(8); //Simple only: Create alt-name pools for varying controls
    private static BulletManager main;
    private Transform spamContainer;
    private const string epLayerName = "HighDirectRender";
    private const string ppLayerName = "LowDirectRender";
    private int epLayerMask;
    private int epRenderLayer;
    private int ppLayerMask;
    private int ppRenderLayer;
    private static GradientMap throwaway_gm;

    private readonly struct DeferredTextureConstruction {
        private readonly Material mat;
        private readonly bool isFrameAnim;
        private readonly SimpleBulletEmptyScript sbes;
        private readonly int renderPriorityOffset;
        private readonly Func<Sprite> SpriteInvoke;
        public readonly bool recolorizable;

        public DeferredTextureConstruction(SimpleBulletEmptyScript sbes, Material mat, int renderPriorityOffset, 
            Func<Sprite> spriteCreator, bool recolorizable) {
            this.mat = mat;
            this.sbes = sbes;
            this.isFrameAnim = sbes.frameAnimInfo.sprite0 != null && sbes.frameAnimInfo.numFrames > 0;
            this.renderPriorityOffset = renderPriorityOffset;
            this.SpriteInvoke = spriteCreator;
            this.recolorizable = recolorizable;
        }
        public MeshGenerator.RenderInfo CreateDeferredTexture() {
            Sprite sprite = SpriteInvoke();
            MeshGenerator.RenderInfo ri = MeshGenerator.RenderInfo.FromSprite(mat, 
                isFrameAnim ? sbes.frameAnimInfo.sprite0 : sprite, 
                sbes.renderPriority + renderPriorityOffset);
            sbes.displacement.SetOnMaterial(ri.material);
            if (recolorizable) ri.material.EnableKeyword("FT_RECOLORIZE");
            if (isFrameAnim) {
                ri.material.EnableKeyword("FT_FRAME_ANIM");
                ri.material.SetFloat(PropConsts.frameT, sbes.frameAnimInfo.framesPerSecond);
                ri.material.SetFloat(PropConsts.frameCt, sbes.frameAnimInfo.numFrames);
                ri.material.SetTexture(PropConsts.mainTex, sprite.texture);
            }
            if (sbes.rotational) {
                ri.material.EnableKeyword("FT_ROTATIONAL");
            }
            var fade = sbes.fadeIn.value;
            if (fade.slideInTime > 0) {
                ri.material.EnableKeyword("FT_SLIDE_IN");
                ri.material.SetFloat(PropConsts.slideInT, fade.slideInTime);
            }
            if (fade.scaleInTime > 0) {
                ri.material.EnableKeyword("FT_SCALE_IN");
                ri.material.SetFloat(PropConsts.scaleInT, fade.scaleInTime);
                ri.material.SetFloat(PropConsts.scaleInMin, fade.scaleInStart);
            }
            if (fade.fadeInTime > 0f) {
                ri.material.EnableKeyword("FT_FADE_IN");
                ri.material.SetFloat(PropConsts.fadeInT, fade.fadeInTime);
            }
            MaterialUtils.SetBlendMode(ri.material, sbes.renderMode);
            return ri;
        }
    }

    private const string SUFF_DARK = "/b";
    private const string SUFF_COLOR = "/";
    private const string SUFF_LIGHT = "/w";
    private static string SUFF_FULL(in DefaultColorizing color) => color.FullInvertedAsLightDark ? SUFF_LIGHT : "/f";
    private static string SUFF_INV(in DefaultColorizing color) => color.FullInvertedAsLightDark ? SUFF_DARK : "/i";

    private INamedGradient[][] ComputePalettes() {
        int nPalettes = basicGradientPalettes.Length;
        var gs = new INamedGradient[nPalettes][];
        for (int ii = 0; ii < nPalettes; ++ii) {
            gs[ii] = new INamedGradient[1+nPalettes];
            gs[ii][0] = basicGradientPalettes[ii];
            for (int jj = 0; jj < nPalettes; ++jj) {
                gs[ii][jj + 1] = new NamedGradient(basicGradientPalettes[ii].Mix(basicGradientPalettes[jj]), 
                    $"{basicGradientPalettes[ii].Name},{basicGradientPalettes[jj].Name}");
            }
        }
        return gs;
    }
    private void RecolorTextures() {
        int nPalettes = basicGradientPalettes.Length;
        INamedGradient[][] computedPalettes = ComputePalettes();
        foreach (var lis in bulletStylesList.prefabs) {
            foreach (DataPrefab x in lis.prefabs) {
                bool isPlayerStyle = x.prefab.GetComponent<PlayerBulletEmptyScript>() != null;
                var sbes = x.prefab.GetComponent<SimpleBulletEmptyScript>();
                if (sbes != null) {
                    var cc = x.prefab.GetComponent<GenericColliderInfo>();
                    void Create(BulletInCode bc) {
                        var targetList = (isPlayerStyle ? activePlayer : activeNpc);
                        if (isPlayerStyle) {
                            AddSimpleStyle(bc.name, new SimpleBulletCollection(targetList, bc));
                        } else if (sbes.TTL > 0) {
                            AddSimpleStyle(bc.name, new DummySBC(targetList, bc, sbes.TTL, sbes.timeRandomization, sbes.rotateRandomization));
                        } else {
                            AddSimpleStyle(bc.name, GetCollectionForColliderType(targetList, bc));
                        }
                    }
                    void CreateN(string cname, int renderPriorityAdd, Func<Sprite> sprite) {
                        Create(new BulletInCode(cname, new DeferredTextureConstruction(sbes, 
                            simpleBulletMaterial, renderPriorityAdd, sprite, false), cc, sbes));
                    }
                    var colors = sbes.colorizing;
                    colors.AssertValidity();
                    Func<Sprite> ColorizeSprite(INamedGradient p, GradientModifier gt) => () => throwaway_gm.Recolor(p.Gradient, gt, sbes.renderMode, sbes.spriteSheet);
                    for (int ii = 0; ii < nPalettes; ++ii) {
                        void CreateP(string cname, int renderPriorityAdd, Func<Sprite> sprite) {
                            Create(new BulletInCode(cname, new DeferredTextureConstruction(sbes, 
                                simpleBulletMaterial, renderPriorityAdd, sprite, 
                                basicGradientPalettes[ii].recolorizable), cc, sbes));
                        }
                        foreach (var p in computedPalettes[ii]) {
                            if (colors.DarkMod.HasValue) CreateP($"{x.name}-{p.Name}{SUFF_DARK}", ii + 0 * nPalettes, ColorizeSprite(p, colors.DarkMod.Value));
                            if (colors.Inverted) CreateP($"{x.name}-{p.Name}{SUFF_INV(in colors)}", ii + 0 * nPalettes, 
                                ColorizeSprite(p, GradientModifier.FULLINV));
                            if (colors.ColorMod.HasValue) CreateP($"{x.name}-{p.Name}{SUFF_COLOR}", ii + 1 * nPalettes, ColorizeSprite(p, colors.ColorMod.Value));
                            if (colors.LightMod.HasValue) CreateP($"{x.name}-{p.Name}{SUFF_LIGHT}", ii + 2 * nPalettes, ColorizeSprite(p, colors.LightMod.Value));
                            if (colors.Full) CreateP($"{x.name}-{p.Name}{SUFF_FULL(in colors)}", ii + 2 * nPalettes, 
                                ColorizeSprite(p, GradientModifier.FULL));
                            if (!colors.TwoPaletteColorings) break;
                        }
                    }
                    
                    //Manual color variants
                    int extras_offset = 3 * nPalettes;
                    foreach (var color in sbes.gradients) {
                        CreateN($"{x.name}-{color.name}".ToLower(), extras_offset++, () => color.gradient.Recolor(sbes.spriteSheet));
                    }
                    foreach (var color in sbes.spriteSpecificGradients) {
                        if (color.gradient != null) {
                            var g = color.gradient;
                            CreateN($"{x.name}-{color.color}".ToLower(), extras_offset++, () => g.Recolor(color.sprite));
                        } else {
                            CreateN($"{x.name}-{color.color}".ToLower(), extras_offset++, () => color.sprite);
                        }
                    }
                    if (!sbes.colorizing.Any && sbes.gradients.Length == 0 && sbes.spriteSpecificGradients.Length == 0) {
                        Log.Unity("No sprite recoloring for "+ x.name);
                        CreateN(x.name, 0, () => sbes.spriteSheet);
                    }
                } else {
                    var fa = x.prefab.GetComponent<Bullet>();
                    var colors = fa.colorizing;
                    colors.AssertValidity();
                    if (!colors.Any && fa.gradients.Length == 0) {
                        //No recoloring. Untested
                        AddFaBStyle(x.name, new DeferredFramesRecoloring(x.prefab, fa, 0, "", x.name, null, false));
                        continue; 
                    }
                    Func<Sprite, Sprite> ColorizeSprite(Palette p, GradientModifier gt) => s => throwaway_gm.Recolor(p.Gradient, gt, fa.renderMode, s);
                    for (int ii = 0; ii < nPalettes; ++ii) {
                        var p = basicGradientPalettes[ii];
                        void CreateF(string suffix, int offset, GradientModifier mod) {
                            var variant = $"{p.colorName}{suffix}";
                            var style = $"{x.name}-{variant}";
                            AddFaBStyle(style, new DeferredFramesRecoloring(x.prefab, fa, ii + offset * nPalettes, 
                                variant, style, ColorizeSprite(p, mod), p.recolorizable));
                        }
                        if (colors.DarkMod.HasValue) 
                            CreateF(SUFF_DARK, 0, colors.DarkMod.Value);
                        if (colors.Inverted) 
                            CreateF(SUFF_INV(in colors), 0, GradientModifier.FULLINV);
                        if (colors.ColorMod.HasValue)
                            CreateF(SUFF_COLOR, 1, colors.ColorMod.Value);
                        if (colors.LightMod.HasValue)
                            CreateF(SUFF_LIGHT, 2, colors.LightMod.Value);
                        if (colors.Full) {
                            CreateF(SUFF_FULL(in colors), 2, GradientModifier.FULL);
                        }
                    }
                    //Manual color variants
                    int extras_offset = 3 * nPalettes;
                    foreach (var color in fa.gradients) {
                        string style = $"{x.name}-{color.name}";
                        AddFaBStyle(style, new DeferredFramesRecoloring(x.prefab, fa, extras_offset++, color.name, 
                            style, color.gradient.Recolor, false));
                    }
                    
                }
            }
        }
        Log.Unity($"Created {simpleBulletPools.Count} bullet styles", level: Log.Level.DEBUG3);
    }

    public const int FAB_PLAYER_RENDER_OFFSET = -1000;
    private class DeferredFramesRecoloring {
        private static readonly Dictionary<FrameRecolorConfig, Sprite> frameCache = new Dictionary<FrameRecolorConfig, Sprite>();
        private FrameAnimBullet.Recolor recolor;
        private bool loaded;

        private readonly Func<Sprite, Sprite> creator;
        private readonly string paletteVariant;
        private readonly int renderPriorityOffset;
        private readonly Bullet b;
        private readonly bool recolorizable;

        public DeferredFramesRecoloring MakePlayerCopy() => new DeferredFramesRecoloring(recolor.prefab, b, 
            renderPriorityOffset + FAB_PLAYER_RENDER_OFFSET, paletteVariant, $"p-{recolor.style}", creator, recolorizable);
        
        public DeferredFramesRecoloring(GameObject prefab, Bullet b, int renderPriorityOffset, string paletteVariant, 
            string style, [CanBeNull] Func<Sprite, Sprite> creator, bool recolorizable) {
            this.b = b;
            this.renderPriorityOffset = renderPriorityOffset;
            this.paletteVariant = paletteVariant;
            this.creator = creator;
            this.recolorizable = recolorizable;
            if (creator == null) { //Don't recolor
                //Pass style in as a parameter instead of trying to access recolor.style, which is not yet set
                recolor = new FrameAnimBullet.Recolor(null, prefab, NewMaterial(style), style);
                loaded = true;
            } else {
                //the material will still be reinstantiated in GetOrLoadRecolor
                recolor = new FrameAnimBullet.Recolor(null, prefab, b.material, style);
            }
        }

        public FrameAnimBullet.Recolor GetOrLoadRecolor() {
            if (!loaded) {
                var frames = b.Frames;
                var sprites = new FrameAnimBullet.BulletAnimSprite[frames.Length];
                for (int si = 0; si < frames.Length; ++si) {
                    sprites[si] = frames[si];
                    //Use variant and not style since multiple prefabs (styles) may share the same sprites,
                    // and we want them to be shared in the cache
                    FrameRecolorConfig frc = new FrameRecolorConfig(frames[si].s.name, paletteVariant);
                    if (frameCache.ContainsKey(frc)) sprites[si].s = frameCache[frc];
                    else sprites[si].s = frameCache[frc] = creator(frames[si].s);
                }
                recolor = new FrameAnimBullet.Recolor(sprites, recolor.prefab, NewMaterial(recolor.style), recolor.style);
                loaded = true;
            }
            return recolor;
        }

        private Material NewMaterial(string style) {
            var m = Instantiate(b.material);
            if (b.fadeInTime > 0f) {
                m.EnableKeyword("FT_FADE_IN");
                m.SetFloat(PropConsts.fadeInT, b.fadeInTime);
            }
            if (recolorizable) m.EnableKeyword("FT_RECOLORIZE");
            m.EnableKeyword("FT_HUESHIFT");
            m.SetFloat(PropConsts.cycleSpeed, b.cycleSpeed);
            if (style.StartsWith("p-")) m.SetFloat(PropConsts.SharedOpacityMul, PLAYER_FB_OPACITY_MUL);
            if (Mathf.Abs(b.cycleSpeed) > 0f) m.EnableKeyword(PropConsts.cycleKW);
            b.displacement.SetOnMaterial(m);
            MaterialUtils.SetBlendMode(m, b.renderMode);
            m.renderQueue += b.renderPriority + renderPriorityOffset;
            return m;
        }
        
    }

    private readonly struct FrameRecolorConfig : IEquatable<FrameRecolorConfig> {
        private readonly string sprite_name;
        private readonly string palette_variant; // eg. "blue/b" or custom name like "black"
        public FrameRecolorConfig(string sprite_name, string palette_variant) {
            this.sprite_name = sprite_name;
            this.palette_variant = palette_variant;
        }

        public bool Equals(FrameRecolorConfig other) {
            return string.Equals(sprite_name, other.sprite_name) &&
                   string.Equals(palette_variant, other.palette_variant);
        }

        public override int GetHashCode() {
            return (sprite_name, palette_variant).GetHashCode();
        }
    }

    public void Setup() {
        BulletManager.main = this;
        PrepareRendering();
        epLayerMask = LayerMask.GetMask(epLayerName);
        epRenderLayer = LayerMask.NameToLayer(epLayerName);
        ppLayerMask = LayerMask.GetMask(ppLayerName);
        ppRenderLayer = LayerMask.NameToLayer(ppLayerName);
        pb = new MaterialPropertyBlock();

        throwaway_gm = ScriptableObject.CreateInstance<GradientMap>();
        ColorScheme.LoadPalettes(basicGradientPalettes);
        RecolorTextures();
        
        SceneIntermediary.RegisterSceneLoad(StartScene);
        Camera.onPreCull += RenderBullets;
    }

    /// <summary>
    /// NPC simple bullets only.
    /// </summary>
    private static SimpleBulletCollection GetCollectionForColliderType(List<SimpleBulletCollection> target, BulletInCode bc) {
        if (bc.cc.colliderType == GenericColliderInfo.ColliderType.Circle) {
            return new CircleSBC(target, bc);
        } else if (bc.cc.colliderType == GenericColliderInfo.ColliderType.Rectangle) {
            return new RectSBC(target, bc);
        } else if (bc.cc.colliderType == GenericColliderInfo.ColliderType.Line) {
            return new LineSBC(target, bc);
        } else if (bc.cc.colliderType == GenericColliderInfo.ColliderType.None) {
            return new NoCollSBC(target, bc);
        }
        throw new NotImplementedException();
    }
}
}