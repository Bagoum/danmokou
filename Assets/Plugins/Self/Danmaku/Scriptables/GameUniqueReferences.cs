﻿using System.Collections.Generic;
using System.Linq;
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
    public IEnumerable<CampaignConfig> Campaigns => new[] {campaign, exCampaign}.Where(c => c != null);
    public DayCampaignConfig dayCampaign;
    public CameraTransitionConfig defaultTransition;
    public SODialogue[] dialogue;
    [Header("Script Keyable")]
    public BossConfig[] bossMetadata;
    public DialogueProfile[] dialogueProfiles;
    public AudioTrack[] tracks;
    public SOPrefabs[] summonables;
    public SOTextAssets[] fileStateMachines;

    private static IEnumerable<PlayerConfig> CampaignShots(CampaignConfig c) =>
        c == null ? new PlayerConfig[0] : c.players; 
    private static IEnumerable<PlayerConfig> CampaignShots(DayCampaignConfig c) =>
        c == null ? new PlayerConfig[0] : c.players;

    public IEnumerable<PlayerConfig> AllPlayers =>
        CampaignShots(campaign).Concat(CampaignShots(exCampaign)).Concat(CampaignShots(dayCampaign));
    public IEnumerable<ShotConfig> AllShots => AllPlayers.SelectMany(x => x.shots);
}