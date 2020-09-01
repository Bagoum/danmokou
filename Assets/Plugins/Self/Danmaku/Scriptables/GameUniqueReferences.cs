using DMath;
using JetBrains.Annotations;
using UnityEngine;
using static Danmaku.Enums;


[CreateAssetMenu(menuName = "Data/Game Data")]
public class GameUniqueReferences : ScriptableObject {
    public string gameIdentifier;
    public Version gameVersion;
    public SceneConfig mainMenu;
    public SceneConfig replaySaveMenu;
    public SceneConfig unitScene;
    public SceneConfig tutorial;
    public SceneConfig miniTutorial;
    public CampaignConfig campaign;
    public CampaignConfig exCampaign;
    public DayCampaignConfig dayCampaign;
    public CameraTransitionConfig defaultTransition;
    public ShotConfig[] shots;
    public SODialogue[] dialogue;
    [Header("Script Keyable")]
    public BossConfig[] bossMetadata;
    public DialogueProfile[] dialogueProfiles;
    public AudioTrack[] tracks;
    public SOPrefabs[] summonables;
    public SOTextAssets[] fileStateMachines;
}