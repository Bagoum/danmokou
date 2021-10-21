using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using Danmokou.Achievements;
using Danmokou.Core;
using UnityEngine;


namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Data/Game Data")]
public class GameUniqueReferences : ScriptableObject {
    [Tooltip("Preferably a short acronym")]
    public string gameIdentifier = "";
    public Version gameVersion;
    public SceneConfig mainMenu = null!;
    public SceneConfig replaySaveMenu = null!;
    public GameObject defaultMenuBackground = null!;
    public Sprite defaultUIFrame = null!;
    public SceneConfig unitScene = null!;
    public SceneConfig? tutorial;
    public SceneConfig? miniTutorial;
    public SceneConfig endcard = null!;
    public CampaignConfig campaign = null!;
    public CampaignConfig? exCampaign;
    public IEnumerable<CampaignConfig> Campaigns => new[] {campaign, exCampaign}.FilterNone();
    public DayCampaignConfig? dayCampaign;
    public CameraTransitionConfig defaultTransition = null!;

    public FieldBounds bounds;

    public AchievementProviderSO? achievements;
    
    public SODialogue[] dialogue = null!;
    [Header("Script Keyable")] public BossConfig[] bossMetadata = null!;
    [Obsolete("Replaced by SZYU-based dialogue handling. Will be removed in DMK v9.")]
    public DialogueProfile[] dialogueProfiles = null!;
    public AudioTrack[] tracks = null!;
    public ItemReferences items = null!;
    public PrefabReferences prefabReferences = null!;
    public SuzunoyaReferences? suzunoyaReferences;
    public UXMLReferences uxmlDefaults = null!;
    public SFXConfigs[] SFX = null!;
    public SOPrefabs[] summonables = null!;
    public SOTextAssets[] fileStateMachines = null!;
    public string[] scriptFolders = null!;

    /// <summary>
    /// If all stage and boss SMs have exactly one untyped phase at the beginning (setup phase 0)
    /// and all other phases are typed, you can enable this to make menu loading faster.
    /// (The setup phase 0 may have properties, as long as they are not `type`.)
    /// </summary>
    public bool fastParsing;

    private static IEnumerable<ShipConfig> CampaignShots(CampaignConfig? c) =>
        c == null ? new ShipConfig[0] : c.players;

    private static IEnumerable<ShipConfig> CampaignShots(DayCampaignConfig? c) =>
        c == null ? new ShipConfig[0] : c.players;

    public IEnumerable<ShipConfig> AllShips =>
        CampaignShots(campaign).Concat(CampaignShots(exCampaign)).Concat(CampaignShots(dayCampaign));
    public IEnumerable<ShotConfig> AllShots => AllShips.SelectMany(x => x.shots2.Select(s => s.shot));

    public IEnumerable<ISupportAbilityConfig> AllSupportAbilities => 
        AllShips.SelectMany(x => x.supports.Select(s => s.ability));

    public ShipConfig FindPlayer(string key) => AllShips.First(p => p.key == key);
    public ShotConfig FindShot(string key) => AllShots.First(s => s.key == key);

    public ISupportAbilityConfig? FindSupportAbility(string key) =>
        AllSupportAbilities.FirstOrDefault(x => x.Key == key);
}
}