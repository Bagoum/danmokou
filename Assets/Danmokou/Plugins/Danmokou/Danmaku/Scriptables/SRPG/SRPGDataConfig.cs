using System;
using System.Collections.Generic;
using Danmokou.Core;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Danmokou.Scriptables {

public interface ISRPGDataConfig {
    IReadOnlyList<ISRPGNodeMatcher> NodeMatchers { get; }
    DataPrefab[] UnitDisplays { get; }
}

[CreateAssetMenu(menuName = "SRPG/Data Config")]
public class SRPGDataConfig : ScriptableObject, ISRPGDataConfig {
    public SRPGNodeMatcher[] nodeMatchers = null!;
    public IReadOnlyList<ISRPGNodeMatcher> NodeMatchers => nodeMatchers;
    [field: SerializeField] 
    public DataPrefab[] UnitDisplays { get; set; } = null!;
}
}