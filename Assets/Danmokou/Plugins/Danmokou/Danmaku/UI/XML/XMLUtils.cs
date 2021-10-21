using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.Functional;
using Danmokou.Achievements;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.GameInstance;
using Danmokou.Scriptables;
using Danmokou.Services;
using JetBrains.Annotations;
using Danmokou.SM;
using UnityEngine.UIElements;
using static Danmokou.Core.LocalizedStrings.Generic;
using static Danmokou.Core.LocalizedStrings.UI;
using static Danmokou.Core.LocalizedStrings.CDifficulty;

namespace Danmokou.UI.XML {
public static class XMLUtils {
    public const string fontUbuntuClass = "font-ubuntu";
    public const string monospaceClass = "monospace";
    public const string large1Class = "large1";
    public const string small1Class = "small1";
    public const string small2Class = "small2";
    public const string small3Class = "small3";
    public const string visibleAdjacentClass = "visibleadjacent";
    public const string optionNoKeyClass = "nokey";
    public const string hideClass = "hide";
    public const string descriptorClass = "descriptor";
    public const string centerTextClass = "centertext";
    public static string CheckmarkClass(bool active) => active ? "checked" : "unchecked";
    
    public static UIScreen ReplayScreen(XMLMenu menu, Action<List<XMLMenu.CacheInstruction>> cacheTentative, Action cacheConfirm) =>
        new LazyUIScreen(menu, () => SaveData.p.ReplayData.Count.Range().Select(i =>
            new CacheNavigateUINode(cacheTentative, () =>
                    SaveData.p.ReplayData.TryN(i)?.metadata.Record.AsDisplay(true, true) ?? generic_deleted,
                new FuncNode(() => {
                    cacheConfirm();
                    return InstanceRequest.ViewReplay(SaveData.p.ReplayData.TryN(i));
                }, replay_view),
                new ConfirmFuncNode(() => SaveData.p.TryDeleteReplay(i), delete, true)
            ).With(monospaceClass).With(small2Class)
        ).ToArray());
    
    public static UIScreen HighScoreScreen(XMLMenu menu, UIScreen replayScreen,
        SMAnalysis.AnalyzedCampaign[] campaigns, SMAnalysis.AnalyzedDayCampaign? days = null) {
        if (campaigns.Length == 0 || campaigns[0].bosses.Length == 0) return new UIScreen(menu, new UINode(scores_nocampaign));
        var replays = new Dictionary<string, int>();
        var mode = InstanceMode.CAMPAIGN;
        var cmpIndex = 0;
        string _campaign;
        string _boss;
        int _bphase;
        int _stage;
        int _sphase;
        bool Matches(ILowInstanceRequestKey key) => mode switch {
            InstanceMode.CAMPAIGN => key is CampaignRequestKey cr && cr.Campaign == _campaign,
            InstanceMode.BOSS_PRACTICE => key is BossPracticeRequestKey br && 
                                          br.Campaign == _campaign && br.Boss == _boss && br.PhaseIndex == _bphase,
            InstanceMode.STAGE_PRACTICE => key is StagePracticeRequestKey sr && 
                                           sr.Campaign == _campaign && sr.StageIndex == _stage && sr.PhaseIndex == _sphase,
            InstanceMode.SCENE_CHALLENGE => key is PhaseChallengeRequestKey sc &&
                                            sc.Campaign == _campaign && sc.Boss == _boss && sc.PhaseIndex == _bphase,
            _ => throw new Exception($"No high score screen handling for key of type {key.GetType()}")
        };
        
        void AssignCampaign(int cmpInd) {
            cmpIndex = cmpInd;
            _campaign = campaigns[cmpIndex].Key;
            AssignStage(0);
            if (campaigns[cmpIndex].bosses.Length > 0)
                AssignBoss(campaigns[cmpIndex].bosses[0].boss.key);
            else
                throw new Exception("No high score handling for days menu implemented yet"); //AssignBoss(days!.bosses[]);
        }
        void AssignBoss(string boss) {
            _boss = boss;
            AssignBossPhase(0);
        }
        void AssignStage(int stage) {
            //Better not to mix with AssignBoss to avoid invalid assignments.
            _stage = stage;
            AssignStagePhase(0);
        }
        void AssignBossPhase(int phase) {
            _bphase = phase;
        }
        void AssignStagePhase(int phase) {
            _sphase = phase;
        }
        AssignCampaign(0);
        SaveData.p.ReplayData.ForEachI((i, r) => replays[r.metadata.RecordUuid] = i);
        var scoreNodes = SaveData.r.FinishedGames.Values
            //If the user doesn't enter a name on the replay screen, the score won't show up, but it will still be recorded internally
            .Where(g => !string.IsNullOrWhiteSpace(g.CustomNameOrPartial) && g.Score > 0)
            .OrderByDescending(g => g.Score).Select(g => {
                //Don't need to show the request (eg. Yukari (Ex) p3) because it's shown by the option nodes above this
                var node = new UINode(g.AsDisplay(true, false));
                if (replays.TryGetValue(g.Uuid, out var i)) node.SetConfirmOverride(() => (true, replayScreen.Top[i]));
                return node.With(monospaceClass).With(small2Class)
                    .With(CheckmarkClass(replays.ContainsKey(g.Uuid)))
                    .VisibleIf(() => Matches(g.RequestKey));
            });
        bool IsBossOrChallenge() => mode == InstanceMode.BOSS_PRACTICE || mode == InstanceMode.SCENE_CHALLENGE;
        bool IsStage() => mode == InstanceMode.STAGE_PRACTICE;
        var optnodes = new[] {
            new OptionNodeLR<InstanceMode>(practice_type, i => mode = i, new[] {
                (practice_m_campaign, InstanceMode.CAMPAIGN),
                (practice_m_boss, InstanceMode.BOSS_PRACTICE),
                days == null ? ((LString, InstanceMode)?) null : (practice_m_scene, InstanceMode.SCENE_CHALLENGE),
                (practice_m_stage, InstanceMode.STAGE_PRACTICE)
            }.FilterNone().ToArray(), mode),
            new OptionNodeLR<int>(practice_campaign, AssignCampaign,
                campaigns.Select((c, i) => (new LString(c.campaign.shortTitle), i)).ToArray(), cmpIndex),
            new DynamicOptionNodeLR<string>(practice_m_whichboss, AssignBoss, () =>
                    IsBossOrChallenge() ?
                        campaigns[cmpIndex].bosses.Select(b => (b.boss.BossPracticeName.Value, b.boss.key)).ToArray() :
                        new[] {("", "")} //required to avoid errors with the option node
                , "").VisibleIf(() => IsBossOrChallenge()),
            new DynamicOptionNodeLR<int>(practice_m_whichstage, AssignStage, () =>
                    IsStage() ?
                        campaigns[cmpIndex].stages.Select((s, i) => (s.stage.stageNumber, i)).ToArray() :
                        new[] {("", 0)} //required to avoid errors with the option node
                , 0).VisibleIf(() => IsStage()),
            new DynamicOptionNodeLR<int>(practice_m_whichphase, AssignBossPhase, () =>
                    IsBossOrChallenge() ?
                        campaigns[cmpIndex].bossKeyMap[_boss].Phases.Select(
                            //p.index is used as request key
                            (p, i) => ($"{i + 1}. {p.Title}", p.index)).ToArray() :
                        new[] {("", 0)}, 0)
                .OnBound(ve => ve.Q("ValueContainer").style.width = new StyleLength(new Length(80, LengthUnit.Percent)))
                .VisibleIf(() => IsBossOrChallenge()),
            new DynamicOptionNodeLR<int>(practice_m_whichphase, AssignStagePhase, () =>
                    IsStage() ?
                        campaigns[cmpIndex].stages[_stage].Phases.Select(
                            p => (p.Title.Value, p.index)).Prepend((practice_fullstage.Value, 1)).ToArray() :
                        new[] {("", 0)}, 0)
                .VisibleIf(() => IsStage()),
        };
        return new UIScreen(menu, optnodes.Append(new PassthroughNode(LString.Empty)).Concat(scoreNodes).ToArray());
    }
    
    
    //TODO: temp workaround for wrapping custom difficulty descriptions
    private static readonly string[] descr = {descriptorClass, "wrap"};

    public static UIScreen CreateCustomDifficultyEdit(XMLMenu menu, VisualTreeAsset screen, 
        Func<DifficultySettings, (bool, UINode)> dfcCont) {
            var load_cbs = new List<Action>();
            var dfc = new DifficultySettings(null);
            void SetNewDFC(DifficultySettings? newDfc) {
                if (newDfc == null) return;
                dfc = FileUtils.CopyJson(newDfc);
                foreach (var cb in load_cbs) {
                    cb();
                }
            }
            double[] _pctMods = {
                0.31, 0.45, 0.58, 0.7, 0.85, 1, 1.2, 1.4, 1.6, 1.8, 2
            };
            var pctMods = _pctMods.Select(x => {
                var offset = (x - 1) * 100;
                var prefix = (offset >= 0) ? "+" : "";
                return (new LString($"{prefix}{offset}%"), x);
            }).ToArray();
            (LString, bool)[] yesNo = {(generic_on, true), (generic_off, false)};
            IEnumerable<(LString, double)> AddPlus(IEnumerable<double> arr) => arr.Select(x => {
                var prefix = (x >= 0) ? "+" : "";
                return (new LString($"{prefix}{x}"), x);
            });
            UINode MakeOption<T>(LString title, IEnumerable<(LString, T)> options, Func<T> deflt, Action<T> apply,
                LString description) {
                var node = new OptionNodeLR<T>(title, apply, options.ToArray(), deflt(), 
                    new UINode(LString.Format("\n\n{0}", description)).With(descr));
                load_cbs.Add(() => node.SetIndexFromVal(deflt()));
                return node.With(small1Class);
            }
            UINode MakePctOption(LString title, Func<double> deflt, Action<double> apply, LString description)
                => MakeOption(title, pctMods, deflt, apply, description);
            UINode MakeOnOffOption(LString title, Func<bool> deflt, Action<bool> apply, LString description)
                => MakeOption(title, yesNo, deflt, apply, description);
            UINode MakeOptionAuto<T>(LString title, IEnumerable<T> options, Func<T> deflt, Action<T> apply, LString description)
                => MakeOption(title, options.Select(x => (new LString(x.ToString()), x)), deflt, apply, description);

            var saved = SaveData.s.DifficultySettings;
            IEnumerable<UINode> MakeSavedDFCNodes(Func<int, UINode> creator, int excess=20) => (saved.Count + excess)
                .Range()
                .Select(i => creator(i)
                    .OnBound(i == 0 ? v => v.style.marginTop = new StyleLength(150) : (Action<VisualElement>?)null)
                    //This can change dynamically
                    .With(ve => {
                        if (saved.TryN(i) == null) ve.AddToClassList(hideClass);
                    })
                    .PassthroughIf(() => saved.TryN(i) == null)
                );
            var newSavedSettingsName = new TextInputNode(new_setting);
            var optSliderHelper = new PassthroughNode(() =>
                desc_effective_ls(effective, DifficultySettings.FancifySlider(dfc.customValueSlider)));
            return new UIScreen(menu, 
                MakeOption(scaling, (DifficultySettings.MIN_SLIDER, DifficultySettings.MAX_SLIDER + 1).Range()
                    .Select(x => (new LString($"{x}"), x)), () => dfc.customValueSlider, dfc.SetCustomDifficulty,
                    desc_scaling),
                optSliderHelper.With(small2Class),
                MakeOptionAuto(suicide, new[] {0, 1, 3, 5, 7}, () => dfc.numSuicideBullets,
                    x => dfc.numSuicideBullets = x, desc_suicide),
                MakePctOption(p_dmg, () => dfc.playerDamageMod, x => dfc.playerDamageMod = x, desc_p_dmg),
                MakePctOption(boss_hp, () => dfc.bossHPMod, x => dfc.bossHPMod = x, desc_boss_hp),
                MakeOnOffOption(respawn, () => dfc.respawnOnDeath, x => dfc.respawnOnDeath = x, desc_respawn),
                MakePctOption(faith_decay, () => dfc.faithDecayMultiplier, x => dfc.faithDecayMultiplier = x, desc_faith_decay),
                MakePctOption(faith_acquire, () => dfc.faithAcquireMultiplier, x => dfc.faithAcquireMultiplier = x, desc_faith_acquire),
                MakePctOption(meter_usage, () => dfc.meterUsageMultiplier, x => dfc.meterUsageMultiplier = x, desc_meter_usage),
                MakePctOption(meter_acquire, () => dfc.meterAcquireMultiplier, x => dfc.meterAcquireMultiplier = x, desc_meter_acquire),
                MakeOnOffOption(bombs_enabled, () => dfc.bombsEnabled, x => dfc.bombsEnabled = x, desc_bombs_enabled),
                MakeOnOffOption(meter_enabled, () => dfc.meterEnabled, x => dfc.meterEnabled = x, desc_meter_enabled),
                MakePctOption(player_speed, () => dfc.playerSpeedMultiplier, x => dfc.playerSpeedMultiplier = x, desc_player_speed),
                MakePctOption(player_hitbox, () => dfc.playerHitboxMultiplier, x => dfc.playerHitboxMultiplier = x, desc_player_hitbox),
                MakePctOption(player_grazebox, () => dfc.playerGrazeboxMultiplier,
                    x => dfc.playerGrazeboxMultiplier = x, desc_player_grazebox),
                MakeOption(lives, (1, 14).Range().Select(x => (new LString($"{x}"), (int?) x)).Prepend((generic_default, null)),
                    () => dfc.startingLives, x => dfc.startingLives = x, desc_lives),
                MakeOption(poc, AddPlus(new[] {
                        //can't use addition to generate these because -6 + 0.4 =/= -5.6...
                        -6, -5.6, -5.2, -4.8, -4.4, -4, -3.6, -3.2, -2.8, -2.4, -2, -1.6, -1.2, -0.8, -0.4,
                        0, 0.4, 0.8, 1.2, 1.6, 2
                    }), () => dfc.pocOffset, x => dfc.pocOffset = x, desc_poc),
                //new PassthroughNode(""),
                new UINode(to_select).SetConfirmOverride(() => dfcCont(dfc)),
                new UINode(save_load_setting,
                    MakeSavedDFCNodes(i => new FuncNode(() => SetNewDFC(saved?.TryN(i)?.settings), 
                        () => new LString(saved?.TryN(i)?.name!).Or(generic_deleted), true))
                        .Append(newSavedSettingsName)
                        .Append(new FuncNode(() => SaveData.s.AddDifficultySettings(newSavedSettingsName.DataWIP, dfc), 
                            save_setting, true))
                        .ToArray()
                ).SetRightChildIndex(-2),
                new UINode(delete_setting, 
                    MakeSavedDFCNodes(i => new ConfirmFuncNode(() => SaveData.s.TryRemoveDifficultySettingsAt(i), 
                        () => new LString(saved?.TryN(i)?.name!).Or(generic_deleted), true)).ToArray()
                ).EnabledIf(() => SaveData.s.DifficultySettings.Count > 0)
            ).With(screen);
        }


    public static UIScreen StatisticsScreen(XMLMenu menu, VisualTreeAsset screen,
        IEnumerable<InstanceRecord> allGames, SMAnalysis.AnalyzedCampaign[] campaigns) {
        InstanceRecord[] games = allGames.ToArray();
        int? campaignIndex;
        Maybe<FixedDifficulty?> difficultySwitch = Maybe<FixedDifficulty?>.None;
        ShipConfig? playerSwitch = null;
        (ShipConfig, ShotConfig)? shotSwitch = null;
        bool Filter(InstanceRecord ir) =>
            (campaignIndex == null ||
             campaigns[campaignIndex.Value].Key == ir.RequestKey.Campaign) &&
            (!difficultySwitch.Valid || difficultySwitch.Value == ir.SharedInstanceMetadata.difficulty.standard) &&
            (playerSwitch == null || playerSwitch == ir.SharedInstanceMetadata.team.ships[0].ship) &&
            (shotSwitch == null || shotSwitch == ir.SharedInstanceMetadata.team.ships[0])
            ;
        string? boss;

        Statistics.StatsGenerator stats;
        void UpdateStats() => 
            stats = new Statistics.StatsGenerator(games.Where(Filter), campaigns, cbp => 
                (campaignIndex == null || (campaigns[campaignIndex.Value].Key == cbp.Campaign)) &&
                (boss == null || (boss == cbp.Boss)));
        
        void AssignCampaign(int? cmpInd) {
            campaignIndex = cmpInd;
            AssignBoss(null);
        }
        void AssignBoss(string? nboss) {
            boss = nboss;
            UpdateStats();
        }
        AssignCampaign(null);

        string AsPct(float f01) => $"{(int) (f01 * 100)}%";
        LString ShowCard((BossPracticeRequest card, float ratio) bpr) {
            return LString.Format(
                "{0} ({1})", 
                bpr.card.phase.Title.FMap(
                    (loc, s) => {
                        var limit = loc switch {
                            Locales.JP => 16,
                            _ => 42
                        };
                        return s.Length > limit ?
                            s.Substring(0, limit-2) + ".." :
                            s;
                    }), 
                new LString(AsPct(bpr.ratio))
            );
        }

        Func<UINode[]> nodes = () => new UINode[] {
            new OptionNodeLR<int?>(practice_campaign, AssignCampaign,
                campaigns
                    .Select((c, i) => (new LString(c.campaign.shortTitle), (int?) i))
                    .Prepend((stats_allcampaigns, null))
                    .ToArray(), campaignIndex),
            new OptionNodeLR<Maybe<FixedDifficulty?>>(stats_seldifficulty, x => {
                    difficultySwitch = x;
                    UpdateStats();
                },
                GameManagement.CustomAndVisibleDifficulties
                    .Select(x => (x?.Describe() ?? difficulty_custom, Maybe<FixedDifficulty?>.Of(x)))
                    .Prepend((stats_alldifficulty, Maybe<FixedDifficulty?>.None)).ToArray(), difficultySwitch),
            new OptionNodeLR<ShipConfig?>(stats_selplayer, x => {
                    playerSwitch = x;
                    UpdateStats();
                },
                GameManagement.References.AllShips
                    .Select(x => (x.ShortTitle, (ShipConfig?) x))
                    .Prepend((stats_allplayers, null)).ToArray(), playerSwitch),
            new OptionNodeLR<(ShipConfig, ShotConfig)?>(stats_selshot, x => {
                    shotSwitch = x;
                    UpdateStats();
                },
                GameManagement.References.AllShips
                    .SelectMany(p => p.shots2
                        .Select(os => (new LString(ShotConfig.PlayerShotDescription(p, os.shot)), 
                            ((ShipConfig, ShotConfig)?) (p, os.shot))))
                    .Prepend((stats_allshots, ((ShipConfig, ShotConfig)?)null)).ToArray(), shotSwitch),

            new TwoLabelUINode(stats_allruns, () => new LString($"{stats.TotalRuns}")),
            new TwoLabelUINode(stats_complete, () => new LString($"{stats.CompletedRuns}")),
            new TwoLabelUINode(stats_1cc, () => new LString($"{stats.OneCCRuns}")),
            new TwoLabelUINode(stats_deaths, () => new LString($"{stats.TotalDeaths}")),
            new TwoLabelUINode(stats_totaltime, () => stats.TotalFrames.FramesToTime()),
            new TwoLabelUINode(stats_avgtime, () => stats.AvgFrames.FramesToTime()),
            new TwoLabelUINode(stats_favday, () => 
                stats.TotalRuns == 0 ? generic_na :
                new LString($"{stats.FavoriteDay.Item1} ({stats.FavoriteDay.Item2.Length})")),
            new TwoLabelUINode(stats_favplayer, () => 
                stats.TotalRuns == 0 ? generic_na :
                    LString.Format(
                "{0} ({1})", stats.FavoriteShip.Item1.ShortTitle, new LString($"{stats.FavoriteShip.Item2.Length}")
            )),
            new TwoLabelUINode(stats_favshot, () => {
                if (stats.TotalRuns == 0) return generic_na;
                var ((pc, sc), recs) = stats.FavoriteShot;
                return LString.Format(new LString("{0} ({1})"), 
                    new LString(ShotConfig.PlayerShotDescription(pc, sc)),
                    new LString($"{recs.Length}")
                );
            }),
            new TwoLabelUINode(stats_highestscore, () => 
                stats.TotalRuns == 0 ? generic_na : new LString($"{stats.MaxScore}")),
            new TwoLabelUINode(stats_capturerate, () => new LString(AsPct(stats.CaptureRate))),
            new TwoLabelUINode(stats_bestcard, () => 
                !stats.HasSpellHist ? generic_na : ShowCard(stats.BestCapture)),
            new TwoLabelUINode(stats_worstcard, () => 
                !stats.HasSpellHist ? generic_na : ShowCard(stats.WorstCapture))
        };
        
        return new LazyUIScreen(menu,
            () => nodes().Select(x => x.With(small1Class)).ToArray()
        ).With(screen);
        
    }

    public static UIScreen AchievementsScreen(XMLMenu menu, VisualTreeAsset screen, VisualTreeAsset node, 
        AchievementManager acvs) =>
        new UIScreen(menu, 
            acvs.SortedAchievements.Select(a => 
                new UINode(a.Title)
                    .OnBound(ev => ev.Q<Label>("Description").text = a.VisibleDescription)
                    .With(CheckmarkClass(a.Completed))
                    .With(node)
                ).ToArray()
            ).With(screen);

    public static UIScreen MusicRoomScreen(XMLMenu menu, VisualTreeAsset screen, IEnumerable<IAudioTrackInfo> musics) =>
        new UIScreen(menu,
            musics.SelectNotNull(m => m.DisplayInMusicRoom switch {
                true => new FuncNode(() => ServiceLocator.Find<IAudioTrackService>().InvokeBGM(m), 
                    new LString(string.Format("({0}) {1}", m.TrackPlayLocation, m.Title)), true,
                        new UINode(m.MusicRoomDescription).With(descr).With(small2Class, fontUbuntuClass)
                    ).SetChildrenInaccessible().With(small1Class),
                false => new UINode("????????????????",
                    new UINode("This track is not yet unlocked.").With(descr).With(small2Class, fontUbuntuClass)
                    ).SetChildrenInaccessible().With(small1Class),
                _ => null
            }).ToArray()
        ).With(screen);

}
}