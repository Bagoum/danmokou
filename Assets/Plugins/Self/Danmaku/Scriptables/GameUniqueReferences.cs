using DMath;
using JetBrains.Annotations;
using UnityEngine;
using static Danmaku.Enums;


[CreateAssetMenu(menuName = "Data/Game Data")]
public class GameUniqueReferences : ScriptableObject {
    public SceneConfig mainMenu;
    public SODialogue[] dialogue;
    [Header("Script Keyable")]
    public BossConfig[] bossMetadata;
    public DialogueProfile[] dialogueProfiles;
    public AudioTrack[] tracks;
    public SOPrefabs[] summonables;
    public SOTextAssets[] fileStateMachines;
}