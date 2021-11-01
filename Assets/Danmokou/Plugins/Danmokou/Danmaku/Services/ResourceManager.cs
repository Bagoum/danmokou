using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Danmokou.Core;
using Danmokou.Scriptables;
using JetBrains.Annotations;
using UnityEngine;


namespace Danmokou.Services {
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

    public NamedEffectStrategy[] effects = null!;
    public NamedBackgroundTransition[] bgTransitions = null!;
    public SOPrefabs backgrounds = null!;
    private static readonly Dictionary<string, EffectStrategy> effectMap = new Dictionary<string, EffectStrategy>();
    private static readonly Dictionary<string, GameObject> Summonables = new Dictionary<string, GameObject>();
    private static readonly Dictionary<string, GameObject> Backgrounds = new Dictionary<string, GameObject>();
    private static readonly Dictionary<string, SOBgTransition> Transitions = new Dictionary<string, SOBgTransition>();

    public void Setup() {
        for (ushort ii = 0; ii < effects.Length; ++ii) {
            effectMap[effects[ii].name] = effects[ii].effect;
        }
        foreach (var tr in bgTransitions) Transitions[tr.name] = tr.transition;
        foreach (var summonables in GameManagement.References.summonables) {
            if (summonables != null) LoadSOPrefabs(summonables, Summonables);
        }
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
        foreach (var b in GameManagement.References.bossMetadata) {
            if (b != null && b.key == key) return b;
        }
        throw new Exception($"No boss configuration exists for key {key}.");
    }

    public static GameObject GetSummonable(string style) {
        if (Summonables.TryGetValue(style, out GameObject prefab)) return prefab;
        throw new Exception($"No summonable by name {style}");
    }

    public static string[] AllSummonableNames => Summonables.Keys.ToArray();

    public static GameObject GetBackground(string style) => Backgrounds.GetOrThrow(style);
    public static SOBgTransition GetBackgroundTransition(string style) => Transitions.GetOrThrow(style);
    public static SOBgTransition WipeTex1 => GetBackgroundTransition("wipetex1");
    public static SOBgTransition Instantaneous => GetBackgroundTransition("instant");
    public static GameObject BlackBG => GetBackground("black");

    public static EffectStrategy GetEffect(string effect) => effectMap.GetOrThrow(effect, "Player fire effects");
}
}