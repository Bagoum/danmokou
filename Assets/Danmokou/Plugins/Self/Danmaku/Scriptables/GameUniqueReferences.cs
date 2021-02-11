using System.Collections.Generic;
using System.Linq;
using DMK.Core;
using UnityEngine;


namespace DMK.Scriptables {
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

    public SODialogue[] dialogue = null!;
    [Header("Script Keyable")] public BossConfig[] bossMetadata = null!;
    public DialogueProfile[] dialogueProfiles = null!;
    public AudioTrack[] tracks = null!;
    public ItemReferences items = null!;
    public PrefabReferences prefabReferences = null!;
    public UXMLReferences uxmlDefaults = null!;
    public SOPrefabs[] summonables = null!;
    public SOTextAssets[] fileStateMachines = null!;
    public string[] scriptFolders = null!;

    private static IEnumerable<PlayerConfig> CampaignShots(CampaignConfig? c) =>
        c == null ? new PlayerConfig[0] : c.players;

    private static IEnumerable<PlayerConfig> CampaignShots(DayCampaignConfig? c) =>
        c == null ? new PlayerConfig[0] : c.players;

    public IEnumerable<PlayerConfig> AllPlayers =>
        CampaignShots(campaign).Concat(CampaignShots(exCampaign)).Concat(CampaignShots(dayCampaign));
    public IEnumerable<ShotConfig> AllShots => AllPlayers.SelectMany(x => x.shots2.Select(s => s.shot));

    public PlayerConfig FindPlayer(string key) => AllPlayers.First(p => p.key == key);
    public ShotConfig FindShot(string key) => AllShots.First(s => s.key == key);
}
}