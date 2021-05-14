using System;
using System.Collections.Generic;
using System.Linq;
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

    public static UIScreen ReplayScreen(Action<List<XMLMenu.CacheInstruction>> cacheTentative, Action cacheConfirm) =>
        new LazyUIScreen(() => SaveData.p.ReplayData.Count.Range().Select(i =>
            new CacheNavigateUINode(cacheTentative, () =>
                    SaveData.p.ReplayData.TryN(i)?.metadata.Record.AsDisplay(true, true) ?? generic_deleted,
                new FuncNode(() => {
                    cacheConfirm();
                    return InstanceRequest.ViewReplay(SaveData.p.ReplayData.TryN(i));
                }, replay_view),
                new ConfirmFuncNode(() => SaveData.p.TryDeleteReplay(i), delete, true)
            ).With(monospaceClass).With(small2Class)
        ).ToArray());


    public static UIScreen HighScoreScreen(UIScreen replayScreen,
        SMAnalysis.AnalyzedCampaign[] campaigns, SMAnalysis.AnalyzedDayCampaign? days = null) {
        if (campaigns.Length == 0) return new UIScreen(new UINode(scores_nocampaign));
        var replays = new Dictionary<string, int>();
        var key = new InstanceRecord().RequestKey;
        var cmpIndex = 0;
        void AssignCampaign(int cmpInd) {
            cmpIndex = cmpInd;
            key.campaign = key.boss.Item1.campaign = key.challenge.Item1.Item1.Item1.campaign =
                key.stage.Item1.campaign = campaigns[cmpIndex].Key;
            AssignStage(0);
            if (campaigns[cmpIndex].bosses.Length > 0)
                AssignBoss(campaigns[cmpIndex].bosses[0].boss.key);
            else
                throw new Exception("No high score handling for days menu implemented yet"); //AssignBoss(days!.bosses[]);
        }
        void AssignBoss(string boss) {
            key.boss.Item1.boss = key.challenge.Item1.Item1.boss = boss;
            AssignBossPhase(0);
        }
        void AssignStage(int stage) {
            //Better not to mix with AssignBoss to avoid invalid assignments.
            key.stage.Item1.stage = stage;
            AssignStagePhase(0);
        }
        void AssignBossPhase(int phase) {
            key.boss.phase = key.challenge.Item1.phase = phase;
        }
        void AssignStagePhase(int phase) {
            key.stage.phase = phase;
        }
        key.stage.phase = 1; //only show full-stage practice
        AssignCampaign(0);
        key.type = 0;
        SaveData.p.ReplayData.ForEachI((i, r) => replays[r.metadata.RecordUuid] = i);
        var scoreNodes = SaveData.r.FinishedGames.Values
            //If the user doesn't enter a name on the replay screen, the score won't show up, but it will still be recorded internally
            .Where(g => !string.IsNullOrWhiteSpace(g.CustomNameOrPartial) && g.Score > 0)
            .OrderByDescending(g => g.Score).Select(g => {
                //Don't need to show the request (eg. Yukari (Ex) p3) because it's shown by the option nodes above this
                var node = new UINode(g.AsDisplay(true, false));
                if (replays.TryGetValue(g.Uuid, out var i)) node.SetConfirmOverride(() => (true, replayScreen.top[i]));
                return node.With(monospaceClass).With(small2Class)
                    .With(CheckmarkClass(replays.ContainsKey(g.Uuid)))
                    .VisibleIf(() => DUHelpers.Tuple4Eq(key, g.RequestKey));
            });
        var optnodes = new UINode[] {
            new OptionNodeLR<short>(practice_type, i => key.type = i, new (LocalizedString, short)?[] {
                (practice_m_campaign, 0),
                (practice_m_boss, 1),
                days == null ? ((LocalizedString, short)?) null : (practice_m_scene, 2),
                (practice_m_stage, 3)
            }.FilterNone().ToArray(), key.type),
            new OptionNodeLR<int>(practice_campaign, AssignCampaign,
                campaigns.Select((c, i) => (new LocalizedString(c.campaign.shortTitle), i)).ToArray(), cmpIndex),
            new DynamicOptionNodeLR<string>(practice_m_whichboss, AssignBoss, () =>
                    key.type == 1 ?
                        campaigns[cmpIndex].bosses.Select(b => (b.boss.BossPracticeName.ValueOrEn, b.boss.key)).ToArray() :
                        new[] {("", "")} //required to avoid errors with the option node
                , "").VisibleIf(() => key.type == 1),
            new DynamicOptionNodeLR<int>(practice_m_whichstage, AssignStage, () =>
                    key.type == 3 ?
                        campaigns[cmpIndex].stages.Select((s, i) => (s.stage.stageNumber, i)).ToArray() :
                        new[] {("", 0)} //required to avoid errors with the option node
                , 0).VisibleIf(() => key.type == 3),
            new DynamicOptionNodeLR<int>(practice_m_whichphase, AssignBossPhase, () =>
                    key.type == 1 ?
                        campaigns[cmpIndex].bossKeyMap[key.boss.Item1.boss].Phases.Select(
                            //p.index is used as request key
                            (p, i) => ($"{i + 1}. {p.Title}", p.index)).ToArray() :
                        new[] {("", 0)}, 0)
                .With(ve => ve.Q("ValueContainer").style.width = new StyleLength(new Length(80, LengthUnit.Percent)))
                .VisibleIf(() => key.type == 1),
            new DynamicOptionNodeLR<int>(practice_m_whichphase, AssignStagePhase, () =>
                    key.type == 3 ?
                        campaigns[cmpIndex].stages[key.stage.Item1.stage].Phases.Select(
                            p => (p.Title.ValueOrEn, p.index)).Prepend((practice_fullstage.ValueOrEn, 1)).ToArray() :
                        new[] {("", 0)}, 0)
                .VisibleIf(() => key.type == 3),
        };
        return new UIScreen(optnodes.Append(new PassthroughNode(LocalizedString.Empty)).Concat(scoreNodes).ToArray());
    }
    
    
    //TODO: temp workaround for wrapping custom difficulty descriptions
    private static readonly string[] descr = {descriptorClass, "wrap"};

    public static UIScreen CreateCustomDifficultyEdit(VisualTreeAsset screen, 
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
                return (new LocalizedString($"{prefix}{offset}%"), x);
            }).ToArray();
            (LocalizedString, bool)[] yesNo = {(generic_on, true), (generic_off, false)};
            IEnumerable<(LocalizedString, double)> AddPlus(IEnumerable<double> arr) => arr.Select(x => {
                var prefix = (x >= 0) ? "+" : "";
                return (new LocalizedString($"{prefix}{x}"), x);
            });
            UINode MakeOption<T>(LocalizedString title, IEnumerable<(LocalizedString, T)> options, Func<T> deflt, Action<T> apply,
                LocalizedString description) {
                var node = new OptionNodeLR<T>(title, apply, options.ToArray(), deflt(), 
                    new UINode(LocalizedString.Format("\n\n{0}", description)).With(descr));
                load_cbs.Add(() => node.SetIndexFromVal(deflt()));
                return node.With(small1Class);
            }
            UINode MakePctOption(LocalizedString title, Func<double> deflt, Action<double> apply, LocalizedString description)
                => MakeOption(title, pctMods, deflt, apply, description);
            UINode MakeOnOffOption(LocalizedString title, Func<bool> deflt, Action<bool> apply, LocalizedString description)
                => MakeOption(title, yesNo, deflt, apply, description);
            UINode MakeOptionAuto<T>(LocalizedString title, IEnumerable<T> options, Func<T> deflt, Action<T> apply, LocalizedString description)
                => MakeOption(title, options.Select(x => (new LocalizedString(x.ToString()), x)), deflt, apply, description);

            var saved = SaveData.s.DifficultySettings;
            IEnumerable<UINode> MakeSavedDFCNodes(Func<int, UINode> creator, int excess=20) => (saved.Count + excess)
                .Range()
                .Select(i => creator(i)
                    .With(i == 0 ? v => v.style.marginTop = new StyleLength(150) : (Action<VisualElement>?)null)
                    .With(ve => {
                        if (saved.TryN(i) == null) ve.AddToClassList(hideClass);
                    })
                    .PassthroughIf(() => saved.TryN(i) == null)
                );
            var newSavedSettingsName = new TextInputNode(new_setting);
            var optSliderHelper = new PassthroughNode(() =>
                desc_effective_ls(effective, DifficultySettings.FancifySlider(dfc.customValueSlider)));
            return new UIScreen(
                MakeOption(scaling, (DifficultySettings.MIN_SLIDER, DifficultySettings.MAX_SLIDER + 1).Range()
                    .Select(x => (new LocalizedString($"{x}"), x)), () => dfc.customValueSlider, dfc.SetCustomDifficulty,
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
                MakeOption(lives, (1, 14).Range().Select(x => (new LocalizedString($"{x}"), (int?) x)).Prepend((generic_default, null)),
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
                        () => LocalizedString.All(saved?.TryN(i)?.name!).Or(generic_deleted), true))
                        .Append(newSavedSettingsName)
                        .Append(new FuncNode(() => SaveData.s.AddDifficultySettings(newSavedSettingsName.DataWIP, dfc), 
                            save_setting, true))
                        .ToArray()
                ).SetRightChildIndex(-2),
                new UINode(delete_setting, 
                    MakeSavedDFCNodes(i => new ConfirmFuncNode(() => SaveData.s.TryRemoveDifficultySettingsAt(i), 
                        () => LocalizedString.All(saved?.TryN(i)?.name!).Or(generic_deleted), true)).ToArray()
                ).EnabledIf(() => SaveData.s.DifficultySettings.Count > 0)
            ).With(screen);
        }


    public static UIScreen StatisticsScreen(VisualTreeAsset screen,
        IEnumerable<InstanceRecord> allGames, SMAnalysis.AnalyzedCampaign[] campaigns) {
        InstanceRecord[] games = allGames.ToArray();
        List<Action> load_cbs = new List<Action>();
        int? campaignIndex;
        Maybe<FixedDifficulty?> difficultySwitch = Maybe<FixedDifficulty?>.None;
        ShipConfig? playerSwitch = null;
        (ShipConfig, ShotConfig)? shotSwitch = null;
        bool Filter(InstanceRecord ir) =>
            (campaignIndex == null ||
             campaigns[campaignIndex.Value].Key == ir.ReconstructedRequestKey.Resolve(
                 c => c,
                 b => b.Item1.Item1,
                 p => p.Item1.Item1.Item1.Item1,
                 s => s.Item1.Item1)) &&
            (!difficultySwitch.valid || difficultySwitch.value == ir.SharedInstanceMetadata.difficulty.standard) &&
            (playerSwitch == null || playerSwitch == ir.SharedInstanceMetadata.team.ships[0].ship) &&
            (shotSwitch == null || shotSwitch == ir.SharedInstanceMetadata.team.ships[0])
            ;
        string? boss;

        Statistics.StatsGenerator stats;
        void UpdateStats() => 
            stats = new Statistics.StatsGenerator(games.Where(Filter), campaigns, cbp => 
                (campaignIndex == null || (campaigns[campaignIndex.Value].Key == cbp.Item1.campaign)) &&
                (boss == null || (boss == cbp.Item1.boss)));
        
        void AssignCampaign(int? cmpInd) {
            campaignIndex = cmpInd;
            AssignBoss(null);
        }
        void AssignBoss(string? nboss) {
            boss = nboss;
            foreach (var cb in load_cbs) cb();
            UpdateStats();
        }
        AssignCampaign(null);

        string AsPct(float f01) => $"{(int) (f01 * 100)}%";
        LocalizedString ShowCard((BossPracticeRequest card, float ratio) bpr) {
            return LocalizedString.Format(
                "{0} ({1})", 
                bpr.card.phase.Title.FMap(
                    s => {
                        var limit = Localization.Locale switch {
                            Locale.JP => 16,
                            _ => 42
                        };
                        return s.Length > limit ?
                            s.Substring(0, limit-2) + ".." :
                            s;
                    }), 
                new LocalizedString(AsPct(bpr.ratio))
            );
        }

        Func<UINode[]> nodes = () => new UINode[] {
            new OptionNodeLR<int?>(practice_campaign, AssignCampaign,
                campaigns
                    .Select((c, i) => (new LocalizedString(c.campaign.shortTitle), (int?) i))
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
                GameManagement.References.AllPlayers
                    .Select(x => (x.ShortTitle, (ShipConfig?) x))
                    .Prepend((stats_allplayers, null)).ToArray(), playerSwitch),
            new OptionNodeLR<(ShipConfig, ShotConfig)?>(stats_selshot, x => {
                    shotSwitch = x;
                    UpdateStats();
                },
                GameManagement.References.AllPlayers
                    .SelectMany(p => p.shots2
                        .Select(os => (new LocalizedString(ShotConfig.PlayerShotDescription(p, os.shot)), 
                            ((ShipConfig, ShotConfig)?) (p, os.shot))))
                    .Prepend((stats_allshots, ((ShipConfig, ShotConfig)?)null)).ToArray(), shotSwitch),

            new TwoLabelUINode(stats_allruns, () => new LocalizedString($"{stats.TotalRuns}")),
            new TwoLabelUINode(stats_complete, () => new LocalizedString($"{stats.CompletedRuns}")),
            new TwoLabelUINode(stats_1cc, () => new LocalizedString($"{stats.OneCCRuns}")),
            new TwoLabelUINode(stats_deaths, () => new LocalizedString($"{stats.TotalDeaths}")),
            new TwoLabelUINode(stats_totaltime, () => stats.TotalFrames.FramesToTime()),
            new TwoLabelUINode(stats_avgtime, () => stats.AvgFrames.FramesToTime()),
            new TwoLabelUINode(stats_favday, () => 
                stats.TotalRuns == 0 ? generic_na :
                new LocalizedString($"{stats.FavoriteDay.Item1} ({stats.FavoriteDay.Item2.Length})")),
            new TwoLabelUINode(stats_favplayer, () => 
                stats.TotalRuns == 0 ? generic_na :
                    LocalizedString.Format(
                "{0} ({1})", stats.FavoriteShip.Item1.ShortTitle, $"{stats.FavoriteShip.Item2.Length}"
            )),
            new TwoLabelUINode(stats_favshot, () => {
                if (stats.TotalRuns == 0) return generic_na;
                var ((pc, sc), recs) = stats.FavoriteShot;
                return LocalizedString.Format(
                    "{0} ({1})", ShotConfig.PlayerShotDescription(pc, sc),
                    $"{recs.Length}"
                );
            }),
            new TwoLabelUINode(stats_highestscore, () => 
                stats.TotalRuns == 0 ? generic_na : new LocalizedString($"{stats.MaxScore}")),
            new TwoLabelUINode(stats_capturerate, () => new LocalizedString(AsPct(stats.CaptureRate))),
            new TwoLabelUINode(stats_bestcard, () => 
                !stats.HasSpellHist ? generic_na : ShowCard(stats.BestCapture)),
            new TwoLabelUINode(stats_worstcard, () => 
                !stats.HasSpellHist ? generic_na : ShowCard(stats.WorstCapture))
        };
        
        return new LazyUIScreen(
            () => nodes().Select(x => x.With(small1Class)).ToArray()
        ).With(screen);
        
    }

    public static UIScreen AchievementsScreen(VisualTreeAsset screen, VisualTreeAsset node, AchievementManager acvs) =>
        new UIScreen(
            acvs.SortedAchievements.Select(a => 
                new UINode(a.Title)
                    .With(ev => ev.Q<Label>("Description").text = a.VisibleDescription)
                    .With(ev => ev.AddToClassList(CheckmarkClass(a.Completed)))
                    .With(node)
                ).ToArray()
            ).With(screen);

    public static UIScreen MusicRoomScreen(VisualTreeAsset screen, IEnumerable<IAudioTrackInfo> musics) =>
        new UIScreen(
            musics.SelectNotNull(m => m.DisplayInMusicRoom switch {
                true => new FuncNode(() => AudioTrackService.InvokeBGM(m), 
                    LocalizedString.Format("({0}) {1}", m.TrackPlayLocation, m.Title), true,
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