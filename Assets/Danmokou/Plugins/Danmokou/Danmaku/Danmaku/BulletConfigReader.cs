﻿using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku.Descriptors;
using Danmokou.DMath;
using Danmokou.Graphics;
using Danmokou.Reflection;
using Danmokou.Scenes;
using Danmokou.Scriptables;
using Danmokou.Services;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine.Profiling;

namespace Danmokou.Danmaku {

/// <summary>
/// A megaclass that manages loading and managing all bullet configuration,
/// updating/rendering simple bullets,
/// and providing standardized interfaces for summoning all bullets.
/// </summary>
public partial class BulletManager : RegularUpdater {
    private const float PLAYER_SB_OPACITY_MUL = 0.5f;
    private const float PLAYER_SB_FADEIN_MUL = 0.4f;
    //It's necessary to scale in player bullets faster since they move faster.
    // In some cases, such as firing slow-moving sun bullets, this may make
    // the shot look awkward, in which case please add a scale option to the bullet.
    private const float PLAYER_SB_SCALEIN_MUL = 0.1f;
    private const float PLAYER_FB_OPACITY_MUL = 0.65f;
    public readonly struct CollidableInfo {
        public readonly GenericColliderInfo.ColliderType colliderType;
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
        public readonly ICollider collider;
        public readonly ApproximatedCircleCollider circleCollider;

        public CollidableInfo(GenericColliderInfo cc) {
            collider = cc.AsCollider;
            circleCollider = cc.AsCircleApproximation;
            colliderType = cc.colliderType;
            radius = cc.radius;
            linePt1 = cc.point1;
            delta = cc.point2 - cc.point1;
            deltaMag2 = delta.sqrMagnitude;
            halfRect = new Vector2(cc.rectHalfX, cc.rectHalfY);
            //same as Collider classes
            if (colliderType == GenericColliderInfo.ColliderType.Rectangle) {
                maxDist2 = cc.rectHalfX * cc.rectHalfX + cc.rectHalfY * cc.rectHalfY;
            } else {
                float maxDist = Mathf.Max(cc.point1.magnitude, cc.point2.magnitude) + radius;
                maxDist2 = maxDist * maxDist;
            }

        }
    }
    
    public record BulletInCode {
        public string name { get; init; }
        private readonly DeferredTextureConstruction deferredRI;
        private bool riLoaded;
        private MeshGenerator.RenderInfo ri;
        public readonly int againstEnemyCooldown;
        public readonly CollidableInfo cc;
        public readonly ushort grazeEveryFrames;
        public OverrideEvented<bool> Destructible { get; private init; }
        public OverrideEvented<bool> Deletable { get; private init; }
        public OverrideEvented<float> CullRadius { get; private init; }
        public OverrideEvented<bool> AllowCameraCull { get; private init; }
        public OverrideEvented<(TP4 black, TP4 white)?> Recolor { get; private init; }
        public OverrideEvented<TP4?> Tint { get; private init; }
        public OverrideEvented<bool> UseZCompare { get; private init; }
        public OverrideEvented<int> RenderQueue { get; private init; }
        public OverrideEvented<int> Damage { get; private init; }
        public DisturbedAnd AllowGraze { get; private init;  }
        public bool Recolorizable => deferredRI.recolorizable;

        public SimpleBulletFader FadeIn => deferredRI.sbes.fadeIn;
        public SimpleBulletFader FadeOut => deferredRI.sbes.FadeOut;

        public BulletInCode(string name, DeferredTextureConstruction dfc, GenericColliderInfo cc,
            SimpleBulletEmptyScript sbes) {
            this.name = name;
            deferredRI = dfc;
            ri = default;
            riLoaded = false;
            againstEnemyCooldown = sbes.framesPerHit;
            this.cc = new CollidableInfo(cc);
            //Minus 1 to allow for zero offset
            grazeEveryFrames = (ushort)(sbes.grazeEveryFrames - 1);
            Destructible = new(sbes.destructible);
            Deletable = new OverrideEvented<bool>(sbes.deletable);
            CullRadius = new OverrideEvented<float>(sbes.screenCullRadius);
            AllowCameraCull = new OverrideEvented<bool>(true);
            Recolor = new OverrideEvented<(TP4, TP4)?>(null);
            Tint = new OverrideEvented<TP4?>(null);
            UseZCompare = new OverrideEvented<bool>(false);
            RenderQueue = new(dfc.RenderQueue);
            Damage = new(sbes.damage);
            AllowGraze = new();
            MakeSubscriptions();
        }

        private void MakeSubscriptions() {
            Tint.Subscribe(tint => {
                if (riLoaded)
                    GetOrLoadRI().Material.SetOrUnsetKeyword(tint != null, PropConsts.tintKW);
            });
            RenderQueue.Subscribe(rq => {
                if (riLoaded)
                    GetOrLoadRI().Material.renderQueue = rq;
            });
        }

        public BulletInCode Copy(string newName) {
            GetOrLoadRI();
            var nbc = this with {
                name = newName,
                ri = ri.Copy(),
                riLoaded = true,
                Destructible = CopyOV(Destructible),
                Deletable = CopyOV(Deletable),
                CullRadius = CopyOV(CullRadius),
                AllowCameraCull = CopyOV(AllowCameraCull),
                Recolor = CopyOV(Recolor),
                Tint = CopyOV(Tint),
                UseZCompare = CopyOV(UseZCompare),
                RenderQueue = CopyOV(RenderQueue),
                Damage = CopyOV(Damage),
                AllowGraze = CopyAnd(AllowGraze)
            };
            nbc.MakeSubscriptions();
            return nbc;
        }

        private static OverrideEvented<T> CopyOV<T>(OverrideEvented<T> baseOV) {
            var w = new OverrideEvented<T>(baseOV.BaseValue);
            w.CopyDisturbances(baseOV);
            return w;
        }
        private static DisturbedAnd CopyAnd(DisturbedAnd baseOV) {
            var w = new DisturbedAnd(baseOV.BaseValue);
            w.CopyDisturbances(baseOV);
            return w;
        }

        public void UseExitFade() {
            DeferredTextureConstruction.SetMaterialFade(GetOrLoadRI(), FadeOut);
        }
        
        public void SetPlayer() {
            ri.Material.SetFloat(PropConsts.scaleInT, PLAYER_SB_SCALEIN_MUL * ri.Material.GetFloat(PropConsts.scaleInT));
            ri.Material.SetFloat(PropConsts.fadeInT, PLAYER_SB_FADEIN_MUL * ri.Material.GetFloat(PropConsts.fadeInT));
            ri.Material.SetFloat(PropConsts.SharedOpacityMul, PLAYER_SB_OPACITY_MUL);
        }

        public MeshGenerator.RenderInfo GetOrLoadRI() {
            if (!riLoaded) {
                ri = deferredRI.CreateDeferredTexture();
                ri.Material.SetOrUnsetKeyword(Tint.Value != null, PropConsts.tintKW);
                ri.Material.renderQueue = RenderQueue.Value;
                riLoaded = true;
            }
            return ri;
        }
    }
    [Serializable]
    public struct GradientVariant {
        public string name;
        public ColorMap gradient;
    }
    public Material simpleBulletMaterial = null!;
    public GameObject emptyBulletPrefab = null!;
    public SOPrefabs bulletStylesList = null!;
    public Palette[] basicGradientPalettes = null!;
    
    /// <summary>
    /// Complex bullets (lasers, pathers). Active pools are stored on BehaviorEntity.activePools.
    /// </summary>
    private static readonly Dictionary<string, BehaviorEntity.BEHStyleMetadata> behPools = new();

    private static void AddComplexStyle(BehaviorEntity.BEHStyleMetadata bsm) {
        behPools[bsm.style ?? throw new Exception("Complex BEHMetadata must have non-null style values")] = bsm;
    }
    private static void AddComplexStyle(DeferredFramesRecoloring dfr) {
        AddComplexStyle(new BehaviorEntity.BEHStyleMetadata(dfr.Style, dfr));
    }

    /// <summary>
    /// Simple bullets. (NPC bullets, copy-pool NPC bullets, most player bullets).
    /// This collection is only updated when pools are created, or copy-pools are deleted.
    /// </summary>
    private static readonly Dictionary<string, SimpleBulletCollection> simpleBulletPools = new();
    public static ICollection<string> SIMPLEBULLETKEYS => simpleBulletPools.Keys;
    private static void AddSimpleStyle(SimpleBulletCollection sbc) {
        simpleBulletPools[sbc.Style] = sbc;
    }

    private static void DestroySimpleStyle(string key) {
        simpleBulletPools.Remove(key);
    }

    
    // Currently activated bullet styles. All styles are deactivated on scene change, and
    // activated when they are used for the first time.

    /// <summary>
    /// NPC bullets, including copied NPC pools but excluding empty bullets.
    /// </summary>
    private static readonly List<SimpleBulletCollection> activeNpc = new(250);
    private static readonly List<SimpleBulletCollection> activePlayer = new(50);
    /// <summary>
    /// All empty bullet pools (EMPTY, copied NPC pools, and any player variants). These are updated first.
    /// </summary>
    private static readonly List<SimpleBulletCollection> activeEmpty = new(8);
    /// <summary>
    /// All culled bullet pools
    /// </summary>
    private static readonly List<SimpleBulletCollection> activeCulled = new(250);
    private static readonly List<SimpleBulletCollection>[] collections = {
        activeEmpty, activeNpc, activePlayer, activeCulled
    };
    private static BulletManager main = null!;
    private Transform spamContainer = null!;
    private const string epLayerName = "HighDirectRender";
    private const string ppLayerName = "LowDirectRender";
    private int epLayerMask;
    private int epRenderLayer;
    private int ppLayerMask;
    private int ppRenderLayer;
    private static GradientMap throwaway_gm = null!;
    private static MultiPaletteMap throwaway_mpm = null!;

    public readonly struct DeferredTextureConstruction {
        private readonly Material baseMat;
        private readonly bool isFrameAnim;
        public readonly SimpleBulletEmptyScript sbes;
        private readonly int renderPriorityOffset;
        private readonly Func<Sprite> SpriteInvoke;
        public readonly bool recolorizable;
        public int RenderQueue => baseMat.renderQueue + sbes.renderPriority + renderPriorityOffset;

        public DeferredTextureConstruction(SimpleBulletEmptyScript sbes, Material baseMat, int renderPriorityOffset, 
            Func<Sprite> spriteCreator, bool recolorizable) {
            this.baseMat = baseMat;
            this.sbes = sbes;
            this.isFrameAnim = sbes.frameAnimInfo.sprite0 != null && sbes.frameAnimInfo.numFrames > 0;
            this.renderPriorityOffset = renderPriorityOffset;
            this.SpriteInvoke = spriteCreator;
            this.recolorizable = recolorizable;
        }

        public static void SetMaterialFade(MeshGenerator.RenderInfo ri, SimpleBulletFader fade) {
            if (fade.slideInTime > 0) {
                ri.Material.EnableKeyword("FT_SLIDE_IN");
                ri.Material.SetFloat(PropConsts.slideInT, fade.slideInTime);
            }
            if (fade.scaleInTime > 0) {
                ri.Material.EnableKeyword("FT_SCALE_IN");
                ri.Material.SetFloat(PropConsts.scaleInT, fade.scaleInTime);
                ri.Material.SetFloat(PropConsts.scaleInMin, fade.scaleInStart);
            }
            if (fade.fadeInTime > 0f) {
                ri.Material.EnableKeyword("FT_FADE_IN");
                ri.Material.SetFloat(PropConsts.fadeInT, fade.fadeInTime);
            }
        }
        public MeshGenerator.RenderInfo CreateDeferredTexture() {
            Sprite sprite = SpriteInvoke();
            MeshGenerator.RenderInfo ri = MeshGenerator.RenderInfo.FromSprite(baseMat, 
                isFrameAnim ? sbes.frameAnimInfo.sprite0 : sprite);
            ri.Material.renderQueue = RenderQueue;
            sbes.displacement.SetOnMaterial(ri.Material);
            if (recolorizable) ri.Material.EnableKeyword("FT_RECOLORIZE");
            if (isFrameAnim) {
                ri.Material.EnableKeyword("FT_FRAME_ANIM");
                ri.Material.SetFloat(PropConsts.frameT, sbes.frameAnimInfo.framesPerSecond);
                ri.Material.SetFloat(PropConsts.frameCt, sbes.frameAnimInfo.numFrames);
                ri.Material.SetTexture(PropConsts.mainTex, sprite.texture);
            }
            if (sbes.rotational) {
                ri.Material.EnableKeyword("FT_ROTATIONAL");
            }
            SetMaterialFade(ri, sbes.fadeIn.value);
            MaterialUtils.SetBlendMode(ri.Material, sbes.renderMode);
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
                    $"{basicGradientPalettes[ii].Name};{basicGradientPalettes[jj].Name}");
            }
        }
        return gs;
    }

    private static void CreateSimpleBulletStyle(SimpleBulletEmptyScript sbes, BulletInCode bc) {
        if (sbes.TTL > 0) {
            AddSimpleStyle(new DummySoftcullSBC(activeNpc, bc, sbes.TTL, sbes.timeRandomization, sbes.rotateRandomization));
        } else {
            AddSimpleStyle(GetCollection(activeNpc, bc));
        }
    }
    private void RecolorTextures() {
        int nPalettes = basicGradientPalettes.Length;
        INamedGradient[][] computedPalettes = ComputePalettes();
        var esbes = emptyBulletPrefab.GetComponent<SimpleBulletEmptyScript>();
        AddSimpleStyle(new EmptySBC(new BulletInCode(EMPTY, 
            new DeferredTextureConstruction(esbes, simpleBulletMaterial, 0, () => esbes.spriteSheet, false), 
            emptyBulletPrefab.GetComponent<GenericColliderInfo>(), esbes)));
        foreach (var lis in bulletStylesList.prefabs) {
            foreach (DataPrefab x in lis.prefabs) {
                var sbes = x.prefab.GetComponent<SimpleBulletEmptyScript>();
                if (sbes != null) {
                    var sbes_mat = sbes.overrideMaterial == null ? simpleBulletMaterial : sbes.overrideMaterial;
                    var cc = x.prefab.GetComponent<GenericColliderInfo>();
                    
                    void CreateN(string cname, int renderPriorityAdd, Func<Sprite> sprite) {
                        CreateSimpleBulletStyle(sbes, 
                            new BulletInCode(cname, new DeferredTextureConstruction(sbes, 
                            sbes_mat, renderPriorityAdd, sprite, false), cc, sbes));
                    }
                    
                    var colors = sbes.colorizing;
                    colors.AssertValidity();
                    
                    void Colorize(string sname, Sprite spritesheet, int renderOffset) {
                        Func<Sprite> ColorizeSprite(INamedGradient p, GradientModifier gt) => () => throwaway_gm.Recolor(p.Gradient, gt, sbes.renderMode, spritesheet);
                        for (int ii = 0; ii < nPalettes; ++ii) {
                            void CreateP(string cname, int renderPriorityAdd, Func<Sprite> sprite) {
                                CreateSimpleBulletStyle(sbes, 
                                    new BulletInCode(cname, new DeferredTextureConstruction(sbes, 
                                    sbes_mat, renderPriorityAdd + renderOffset, sprite, 
                                    basicGradientPalettes[ii].recolorizable), cc, sbes));
                            }
                            foreach (var p in computedPalettes[ii]) {
                                if (colors.DarkMod.HasValue) CreateP($"{sname}-{p.Name}{SUFF_DARK}", ii + 0 * nPalettes, ColorizeSprite(p, colors.DarkMod.Value));
                                if (colors.Inverted) CreateP($"{sname}-{p.Name}{SUFF_INV(in colors)}", ii + 0 * nPalettes, 
                                    ColorizeSprite(p, GradientModifier.FULLINV));
                                if (colors.ColorMod.HasValue) CreateP($"{sname}-{p.Name}{SUFF_COLOR}", ii + 1 * nPalettes, ColorizeSprite(p, colors.ColorMod.Value));
                                if (colors.LightMod.HasValue) CreateP($"{sname}-{p.Name}{SUFF_LIGHT}", ii + 2 * nPalettes, ColorizeSprite(p, colors.LightMod.Value));
                                if (colors.Full) CreateP($"{sname}-{p.Name}{SUFF_FULL(in colors)}", ii + 2 * nPalettes, 
                                    ColorizeSprite(p, GradientModifier.FULL));
                                if (!colors.TwoPaletteColorings) break;
                            }
                            //Multi-channel
                            if (colors.MultiChannelRecolor == RGBRecolorMode.RB) {
                                var r = basicGradientPalettes[ii];
                                for (int jj = 0; jj < nPalettes; ++jj) {
                                    var b = basicGradientPalettes[jj];
                                    var bname = $"{sname}-{r.Name};{b.Name}";
                                    if (colors.DarkMod.Try(out var d))
                                        CreateP($"{bname}{SUFF_DARK}", ii + 0 * nPalettes, () => 
                                            throwaway_mpm.Recolor(r, d, r, d, b, d, sbes.renderMode, spritesheet));
                                    if (colors.ColorMod.Try(out var c))
                                        CreateP($"{bname}{SUFF_COLOR}", ii + 1 * nPalettes, () => 
                                            throwaway_mpm.Recolor(r, c, r, c, b, c, sbes.renderMode, spritesheet));
                                    if (colors.LightMod.Try(out var l))
                                        CreateP($"{bname}{SUFF_LIGHT}", ii + 2 * nPalettes, () => 
                                            throwaway_mpm.Recolor(r, l, r, l, b, l, sbes.renderMode, spritesheet));
                                }
                            }
                        }
                        
                        //Manual color variants
                        int extras_offset = 3 * nPalettes;
                        foreach (var color in sbes.gradients) {
                            CreateN($"{sname}-{color.name}".ToLower(), extras_offset++, () => color.gradient.Recolor(spritesheet, sbes.renderMode));
                        }
                        if (!sbes.colorizing.Any && sbes.gradients.Length == 0) {
                            Logs.Log("No sprite recoloring for "+ sname);
                            CreateN(sname, 0, () => spritesheet);
                        }
                    }
                    
                    Colorize(x.name, sbes.spriteSheet, 0);
                    foreach (var form in sbes.identicalForms) {
                        Colorize(form.name, form.sprite, form.renderOffset);
                    }
                    int extras_offset_outer = 3 * nPalettes;
                    foreach (var color in sbes.spriteSpecificGradients) {
                        if (color.gradient != null) {
                            var g = color.gradient;
                            CreateN($"{x.name}-{color.color}".ToLower(), extras_offset_outer++, () => g.Recolor(color.sprite, sbes.renderMode));
                        } else {
                            CreateN($"{x.name}-{color.color}".ToLower(), extras_offset_outer++, () => color.sprite);
                        }
                    }

                } else {
                    var fa = x.prefab.GetComponent<Bullet>();
                    var colors = fa.colorizing;
                    colors.AssertValidity();
                    if (!colors.Any && fa.gradients.Length == 0) {
                        //No recoloring. Untested
                        AddComplexStyle(new DeferredFramesRecoloring(x.prefab, fa, 0, "", x.name, null, false));
                        continue; 
                    }
                    Func<Sprite, Sprite> ColorizeSprite(Palette p, GradientModifier gt) => s => throwaway_gm.Recolor(p.Gradient, gt, fa.renderMode, s);
                    for (int ii = 0; ii < nPalettes; ++ii) {
                        var p = basicGradientPalettes[ii];
                        void CreateF(string suffix, int offset, GradientModifier mod) {
                            var variant = $"{p.colorName}{suffix}";
                            var style = $"{x.name}-{variant}";
                            AddComplexStyle(new DeferredFramesRecoloring(x.prefab, fa, ii + offset * nPalettes, 
                                variant, style, ColorizeSprite(p, mod), p.recolorizable, p));
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
                        //Multi-channel
                        if (colors.MultiChannelRecolor == RGBRecolorMode.RB) {
                            var r = basicGradientPalettes[ii];
                            for (int jj = 0; jj < nPalettes; ++jj) {
                                var b = basicGradientPalettes[jj];
                                void Create2F(string suffix, int offset, Func<Sprite, Sprite> recolorer) {
                                    var variant = $"{r.Name};{b.Name}{suffix}";
                                    var style = $"{x.name}-{variant}";
                                    AddComplexStyle(new DeferredFramesRecoloring(x.prefab, fa, ii + offset * nPalettes, 
                                        variant, style, recolorer, p.recolorizable, p));
                                }
                                if (colors.DarkMod.Try(out var d))
                                    Create2F(SUFF_DARK, 0, s => 
                                        throwaway_mpm.Recolor(r, d, r, d, b, d, fa.renderMode, s));
                                if (colors.ColorMod.Try(out var c))
                                    Create2F(SUFF_COLOR, 1, s => 
                                        throwaway_mpm.Recolor(r, c, r, c, b, c, fa.renderMode, s));
                                if (colors.LightMod.Try(out var l))
                                    Create2F(SUFF_LIGHT, 2, s => 
                                        throwaway_mpm.Recolor(r, l, r, l, b, l, fa.renderMode, s));
                            }
                        }
                    }
                    //Manual color variants
                    int extras_offset = 3 * nPalettes;
                    foreach (var color in fa.gradients) {
                        string style = $"{x.name}-{color.name}";
                        AddComplexStyle(new DeferredFramesRecoloring(x.prefab, fa, extras_offset++, color.name, 
                            style, s => color.gradient.Recolor(s, fa.renderMode), false));
                    }
                    
                }
            }
        }
        Logs.Log($"Created {simpleBulletPools.Count} bullet styles", level: LogLevel.DEBUG3);
    }

    public const int FAB_PLAYER_RENDER_OFFSET = -1000;
    public class DeferredFramesRecoloring {
        private static readonly Dictionary<FrameRecolorConfig, Sprite> frameCache = new();
        private FrameAnimBullet.Recolor recolor;
        public string Style => recolor.style;
        private bool loaded;

        private readonly Func<Sprite, Sprite>? creator;
        private readonly string paletteVariant;
        private readonly int renderPriorityOffset;
        private readonly Bullet b;
        private readonly bool recolorizable;
        private readonly bool player;
        public readonly Palette? palette;

        public DeferredFramesRecoloring MakePlayerCopy() => new(recolor.prefab, b, 
            renderPriorityOffset + FAB_PLAYER_RENDER_OFFSET, paletteVariant, $"{PLAYERPREFIX}{recolor.style}", creator, recolorizable,  palette, true);
        
        public DeferredFramesRecoloring(GameObject prefab, Bullet b, int renderPriorityOffset, string paletteVariant, 
            string style, Func<Sprite, Sprite>? creator, bool recolorizable, 
            Palette? palette = null, bool player=false) {
            this.b = b;
            this.renderPriorityOffset = renderPriorityOffset;
            this.paletteVariant = paletteVariant;
            this.creator = creator;
            this.recolorizable = recolorizable;
            this.palette = palette;
            this.player = player;
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
                Profiler.BeginSample("Frame-anim bullet recolor loading");
                var fb = b as FrameAnimBullet;
                var frames = fb != null ? fb.Frames : new FrameAnimBullet.BulletAnimSprite[0];
                var sprites = new FrameAnimBullet.BulletAnimSprite[frames.Length];
                for (int si = 0; si < frames.Length; ++si) {
                    sprites[si] = frames[si];
                    //Use variant and not style since multiple prefabs (styles) may share the same sprites,
                    // and we want them to be shared in the cache
                    FrameRecolorConfig frc = new FrameRecolorConfig(frames[si].s.name, paletteVariant);
                    if (frameCache.ContainsKey(frc)) sprites[si].s = frameCache[frc];
                    else sprites[si].s = frameCache[frc] = creator!(frames[si].s);
                }
                recolor = new FrameAnimBullet.Recolor(sprites, recolor.prefab, NewMaterial(recolor.style), recolor.style);
                loaded = true;
                Profiler.EndSample();
            }
            return recolor;
        }

        private Material NewMaterial(string style) {
            var m = Instantiate(b.material);
            if (b.fadeInTime > 0f) {
                PropConsts.fadeInKW.Enable(m);
                m.SetFloat(PropConsts.fadeInT, b.fadeInTime);
            }
            if (recolorizable) m.EnableKeyword("FT_RECOLORIZE");
            PropConsts.hueShiftKW.Enable(m);
            m.SetFloat(PropConsts.cycleSpeed, b.cycleSpeed);
            if (player) m.SetFloat(PropConsts.SharedOpacityMul, PLAYER_FB_OPACITY_MUL);
            if (Mathf.Abs(b.cycleSpeed) > 0f) 
                PropConsts.cycleKW.Enable(m);
            b.displacement.SetOnMaterial(m);
            MaterialUtils.SetBlendMode(m, b.renderMode);
            m.renderQueue += b.renderPriority + renderPriorityOffset;
            return m;
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
    }

    public void Setup() {
        BulletManager.main = this;
        epLayerMask = LayerMask.GetMask(epLayerName);
        epRenderLayer = LayerMask.NameToLayer(epLayerName);
        ppLayerMask = LayerMask.GetMask(ppLayerName);
        ppRenderLayer = LayerMask.NameToLayer(ppLayerName);
        pb = new MaterialPropertyBlock();

        throwaway_gm = ScriptableObject.CreateInstance<GradientMap>();
        throwaway_mpm = ScriptableObject.CreateInstance<MultiPaletteMap>();
        ColorScheme.LoadPalettes(basicGradientPalettes);
        RecolorTextures();
        foreach (var style in ResourceManager.AllSummonableNames) {
            AddComplexStyle(new BehaviorEntity.BEHStyleMetadata(style, null));
        }
        
        SceneIntermediary.SceneLoaded.Subscribe(_ => StartScene());
        Camera.onPreCull += RenderBullets;
    }

    public override void FirstFrame() {
        SetupRendering();
        base.FirstFrame();
    }

    /// <summary>
    /// NPC simple bullets only.
    /// </summary>
    private static SimpleBulletCollection GetCollection(List<SimpleBulletCollection> target, BulletInCode bc) {
        if (bc.name == BulletFlakeName)
            return new BulletFlakeSBC(target, bc);
        return bc.cc.colliderType switch {
            GenericColliderInfo.ColliderType.Circle => new CircleSBC(target, bc),
            GenericColliderInfo.ColliderType.Rectangle => new RectSBC(target, bc),
            GenericColliderInfo.ColliderType.Line => new LineSBC(target, bc),
            GenericColliderInfo.ColliderType.None => new NoCollSBC(target, bc),
            _ => throw new NotImplementedException()
        };
    }

    public const string BulletFlakeName = "$flakeBulletClear";
    private static readonly ReflWrap<VTP> FlakeMovement = new(
        "nrvelocity(b{\n" +
            "var mt = ss0(lerp(2, 12, distto(lplayertrue), 0.5, 1.4));\n" +
            "switchH(t, mt, py(lerpt(0, mt, 0.4, 0)), vhome(lerpt(0, 0.2, 4, 14), lplayertrue));" +
        "})");
    private static readonly ReflWrap<SBV2> FlakeRot = new("cx(1)");
}
}