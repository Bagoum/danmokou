using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using Danmokou.Achievements;
using Danmokou.Core;
using Danmokou.Danmaku;
using UnityEngine;


namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Data/Game Data")]
public class GameUniqueReferences : ScriptableObject {
    public GameDef gameDefinition = null!;
    public ICampaignDanmakuGameDef CampaignGameDef => gameDefinition is ICampaignDanmakuGameDef g ?
        g :
        throw new Exception($"The game {gameDefinition.Key} does not support {nameof(ICampaignDanmakuGameDef)}");
    public ISceneDanmakuGameDef SceneGameDef => gameDefinition is ISceneDanmakuGameDef g ?
        g :
        throw new Exception($"The game {gameDefinition.Key} does not support {nameof(ISceneDanmakuGameDef)}");
    public SceneConfig mainMenu = null!;
    public Sprite defaultUIFrame = null!;
    public SceneConfig unitScene = null!;
    public CameraTransitionConfig defaultTransition = null!;

    public SODialogue[] dialogue = null!;
    [Header("Script Keyable")] public BossConfig[] bossMetadata = null!;
    public AudioTrack[] tracks = null!;
    public ItemReferences items = null!;
    public PrefabReferences prefabReferences = null!;
    public SuzunoyaReferences? suzunoyaReferences;
    public ADVReferences? advReferences;
    public UXMLReferences uxmlDefaults = null!;
    public SFXConfigs[] SFX = null!;
    public SOPrefabs[] summonables = null!;
    public SOTextAssets[] fileStateMachines = null!;
    public NamedTextAsset[] licenses = null!;
#if UNITY_EDITOR
    [Tooltip("List of folders containing .bdsl script files. Used for IL2CPP compilation.")]
    public string[] scriptFolders = null!;
#endif

    /// <summary>
    /// If all stage and boss SMs have exactly one untyped phase at the beginning (setup phase 0)
    /// and all other phases are typed, you can enable this to make menu loading faster.
    /// (The setup phase 0 may have properties, as long as they are not `type`.)
    /// </summary>
    public bool fastParsing;

}
}