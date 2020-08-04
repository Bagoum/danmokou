using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

public class ResourceManager : MonoBehaviour {
    [Serializable]
    public struct NamedEffectStrategy {
        public string name;
        public EffectStrategy effect;
    }

    [Serializable]
    public struct NamedBackgroundTransition {
        public string name;
        public SOBgTransition transition;
    }

    private static ResourceManager main;
    public NamedEffectStrategy[] effects;
    public NamedBackgroundTransition[] bgTransitions;
    public SOPrefabs summonables;
    public SOPrefabs backgrounds;
    /// <summary>
    /// Boss metadata used by PatternProperties.
    /// </summary>
    public BossConfig[] bossMetadata;
    private static readonly Dictionary<string, EffectStrategy> effectMap = new Dictionary<string, EffectStrategy>();
    private static readonly Dictionary<string, GameObject> BehStyles = new Dictionary<string, GameObject>();
    private static readonly Dictionary<string, GameObject> Backgrounds = new Dictionary<string, GameObject>();
    private static readonly Dictionary<string, SOBgTransition> Transitions = new Dictionary<string, SOBgTransition>();

    private void Awake() {
        main = this;
        for (ushort ii = 0; ii < effects.Length; ++ii) {
            effectMap[effects[ii].name] = effects[ii].effect;
        }
        foreach (var tr in bgTransitions) Transitions[tr.name] = tr.transition;
        LoadSOPrefabs(summonables, BehStyles);
        LoadSOPrefabs(backgrounds, Backgrounds);
    }

    private static void LoadSOPrefabs(SOPrefabs source, IDictionary<string, GameObject> target) {
        foreach (var lis in source.prefabs) {
            foreach (DataPrefab x in lis.prefabs) {
                target[x.name] = x.prefab;
            }
        }
    }

    public static BossConfig GetBoss(string key) {
        foreach (var b in main.bossMetadata) {
            if (b  != null && b.key == key) return b;
        }
        throw new Exception($"No boss configuration exists for key {key}.");
    }

    public static GameObject GetBEHPrefab(string style) {
        if (BehStyles.TryGetValue(style, out GameObject prefab)) return prefab;
        throw new Exception($"No summonable by name {style}");
    }

    public static GameObject GetBackground(string style) => Backgrounds.GetOrThrow(style);
    public static SOBgTransition GetBackgroundTransition(string style) => Transitions.GetOrThrow(style);
    public static SOBgTransition WipeTex1 => GetBackgroundTransition("wipetex1");
    public static GameObject BlackBG => GetBackground("black");

    public static EffectStrategy GetEffect(string effect) => effectMap.GetOrThrow(effect, "Player fire effects");
    
}