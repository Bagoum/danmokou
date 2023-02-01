using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Culture;
using BagoumLib.Functional;
using Danmokou.Achievements;
using Danmokou.ADV;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Core.DInput;
using Danmokou.Danmaku;
using Danmokou.GameInstance;
using Danmokou.Graphics.Backgrounds;
using Danmokou.Player;
using Danmokou.Scriptables;
using Danmokou.Services;
using Danmokou.SM;
using Danmokou.VN;
using Mizuhashi.Parsers;
using UnityEngine;
using UnityEngine.UIElements;
using static Danmokou.Core.LocalizedStrings.Generic;
using static Danmokou.Core.LocalizedStrings.UI;
using static Danmokou.Core.LocalizedStrings.CDifficulty;
using static Danmokou.Services.GameManagement;
using static Danmokou.UI.PlayModeCommentator;
using static Danmokou.UI.XML.XMLUtils;

namespace Danmokou.UI.XML {
public static class XMLHelpers {
    public static UXMLReferences Prefabs => GameManagement.UXMLPrefabs;
    
    /// <summary>
    /// Configure a DMK prefab-based background for use with a <see cref="UIScreen"/>.
    /// </summary>
    public static UIScreen WithBG(this UIScreen screen,
        (GameObject prefab, BackgroundTransition transition)? background) {
        if (background.Try(out var bg)) {
            screen.OnEnterStart = screen.OnEnterStart.Then(fromNull => {
                var bgo = ServiceLocator.FindOrNull<IBackgroundOrchestrator>();
                bgo?.QueueTransition(bg.transition);
                bgo?.ConstructTarget(bg.prefab, !fromNull);
            });
        }
        return screen;
    }
    
    public static UIScreen PlaymodeScreen(this UIController m, ICampaignDanmakuGameDef game, UIScreen bossPractice, UIScreen stagePractice, Dictionary<Mode, Sprite> sprites, PlayModeCommentator? commentator, Func<CampaignConfig, Func<SharedInstanceMetadata, bool>, Func<UINode, UIResult>> getMetadata) {
        var floater = References.uxmlDefaults.FloatingNode;
        var s = new UIScreen(m, null, UIScreen.Display.Unlined) {
            Builder = (s, ve) => ve.CenterElements()
        };
        Action<UINode> Builder(Mode mode) => n => XMLUtils.ConfigureFloatingImage(n.NodeHTML, sprites[mode]);
        bool tutorialIncomplete = !SaveData.r.TutorialDone && game.Tutorial != null;
        PlayModeStatus Wrap(Mode m, bool locked) =>
            new PlayModeStatus(m, locked) { TutorialIncomplete = tutorialIncomplete };
        s.SetFirst(new CommentatorAxisColumn<PlayModeStatus>(s, new UIRenderDirect(s), new[] {
            (new UINode() {
                OnConfirm = getMetadata(game.Campaign, meta => 
                    InstanceRequest.RunCampaign(MainCampaign, null, meta)),
                Prefab = floater,
                OnBuilt = Builder(Mode.MAIN)
            }, Wrap(Mode.MAIN, false)),
            (game.ExCampaign != null ? 
                new UINode() {
                    EnabledIf = () => SaveData.r.CampaignCompleted(game.Campaign.Key),
                    OnConfirm = getMetadata(game.ExCampaign, meta => 
                        InstanceRequest.RunCampaign(ExtraCampaign, null, meta)),
                    Prefab = floater,
                    OnBuilt = Builder(Mode.EX)
                } : null, 
                Wrap(Mode.EX, !SaveData.r.CampaignCompleted(game.Campaign.Key))),
            (PracticeBossesExist ?
                new UINode() {
                    EnabledIf = () => PBosses.Length > 0,
                    OnConfirm = _ => new UIResult.GoToNode(bossPractice),
                    Prefab = floater,
                    OnBuilt = Builder(Mode.BOSSPRAC)
                } : null,
                Wrap(Mode.BOSSPRAC, PBosses.Length == 0)),
            (PracticeStagesExist ?
                new UINode() {
                    EnabledIf = () => PStages.Length > 0,
                    OnConfirm = _ => new UIResult.GoToNode(stagePractice),
                    Prefab = floater,
                    OnBuilt = Builder(Mode.STAGEPRAC)
                } : null,
                Wrap(Mode.STAGEPRAC, PStages.Length == 0)),
            (game.Tutorial != null ? new UINode {
                OnConfirm = _ => new UIResult.StayOnNode(!InstanceRequest.RunTutorial(game)),
                Prefab = floater,
                OnBuilt = Builder(Mode.TUTORIAL)
            } : null, Wrap(Mode.TUTORIAL, false))
        }) {
            EntryIndexOverride = () => tutorialIncomplete ? -1 : 0,
            Commentator = commentator
        });
        return s;
    }
    public static UIScreen StagePracticeScreen(this UIController m,
        Func<CampaignConfig, Func<SharedInstanceMetadata, bool>, Func<UINode, UIResult>> getMetadata) {
        var s = new UIScreen(m, "STAGE PRACTICE") {Builder = (s, ve) => {
            s.Margin.SetLRMargin(720, 720);
            ve.AddColumn().style.flexGrow = 2;
            ve.AddColumn().style.flexGrow = 5;
        }};
        var stageSel1 = s.ColumnRender(1);
        s.SetFirst(new UIColumn(s, null) {
            LazyNodes = () => GameManagement.PStages.Select(stage => 
                new UINode(practice_stage_ls(stage.stage.stageNumber)) {
                    ShowHideGroup = new UIColumn(stageSel1, 
                        stage.Phases.Select(phase =>
                            new UINode(phase.Title) {
                                CacheOnEnter = true,
                                OnConfirm = getMetadata(stage.campaign.campaign, meta => {
                                    m.ConfirmCache();
                                    return new InstanceRequest(InstanceRequest.PracticeSuccess, meta, new StagePracticeRequest(stage, phase.index))
                                        .Run();
                                })
                            }).Prepend(
                            new UINode(practice_fullstage) {
                                CacheOnEnter = true,
                                OnConfirm = getMetadata(stage.campaign.campaign, meta => {
                                    m.ConfirmCache();
                                    return new InstanceRequest(InstanceRequest.PracticeSuccess, meta, new StagePracticeRequest(stage, 1))
                                        .Run();
                                })
                            }
                        )
                    )
                })
        });
        return s;
    }

    public static UIScreen BossPracticeScreen(this UIController m, VisualTreeAsset spellPracticeNodeV,
        Func<CampaignConfig, Func<SharedInstanceMetadata, bool>, Func<UINode, UIResult>> getMetadata) {
        var cmpSpellHist = SaveData.r.GetCampaignSpellHistory();
        var prcSpellHist = SaveData.r.GetPracticeSpellHistory();

        var s = new UIScreen(m, "BOSS PRACTICE") {Builder = (_, ve) => {
            ve.AddScrollColumn().style.flexGrow = 2f;
            ve.AddScrollColumn().style.flexGrow = 5f;
        }};
        var bossSel1 = s.ColumnRender(1);
        s.SetFirst(new UIColumn(s, null) {
            LazyNodes = () => GameManagement.PBosses.Select(boss =>
                new UINode(boss.boss.BossPracticeName) {
                    ShowHideGroup = new UIColumn(bossSel1, boss.Phases.Select(phase => {
                        var req = new BossPracticeRequest(boss, phase);
                        var key = (req.Key as BossPracticeRequestKey)!;
                        return new UINode(phase.Title) {
                            Prefab = spellPracticeNodeV,
                            OnBuilt = n => {
                                var (cs, ct) = cmpSpellHist.GetOrDefault(key) ?? (0, 0);
                                var (ps, pt) = prcSpellHist.GetOrDefault(key) ?? (0, 0);
                                n.NodeHTML.Q<Label>("CampaignHistory").text = $"{cs}/{ct}";
                                n.NodeHTML.Q<Label>("PracticeHistory").text = $"{ps}/{pt}";
                            },
                            CacheOnEnter = true,
                            OnConfirm = getMetadata(boss.campaign.campaign, meta => {
                                m.ConfirmCache();
                                return new InstanceRequest(InstanceRequest.PracticeSuccess, meta, req).Run();
                            })
                        };
                    }))
                })
        });
        return s;
    }
    
    private static (LString, bool)[] OnOffOption => new[] {
        (generic_on, true),
        (generic_off, false)
    };
    public static UIScreen OptionsScreen(this UIController m, bool allowStaticOptions) {
        var s = new UIScreen(m, null) {
            OnExitEnd = SaveData.AssignSettingsChanges,
            Builder = (s, _) => {
                s.HTML.Q("HeaderRow").SetLRMargin(-80, -80);
            }
        };
        //To support a setup where the top row does not scroll, we do as follows:
        //Controls container (column)
        // - Top row (row)
        // - Controls space (scroll column)
        //   - Binding rows ([row])
        var controlsContainer =
            new UIRenderConstructed(s, Prefabs.UIScreenColumn, (_, ve) => ve.style.width = 100f.Percent());
        var controlsHeader = new UIRenderConstructed(controlsContainer, Prefabs.UIScreenRow);
        var controlsSpace = new UIRenderConstructed(controlsContainer, Prefabs.UIScreenScrollColumn,
            (_, ve) => {
                ve.style.width = new Length(100, LengthUnit.Percent);
                var scrollBox = ve.Q(null, "unity-scroll-view__content-viewport");
                scrollBox.style.paddingLeft = 0;
                scrollBox.style.paddingRight = 0;
            }).ColumnRender(0);
        UINode NodeForBinding(RebindableInputBinding b, int index, KeyRebindInputNode.Mode mode) {
            return new FuncNode(() => (b.Sources[index]?.Description ?? "(No binding)"), n => {
                if (b.ProtectedIndices.Contains(index))
                    return PopupUIGroup.LRB2(n, () => $"Keybinding for \"{b.Purpose}\"",
                        r => new UIColumn(r,
                                new UINode("You cannot rebind this key.") { Prefab = Prefabs.PureTextNode })
                            { Interactable = false },
                        null, Array.Empty<UINode>());
                
                Maybe<IInspectableInputBinding>? newTempBinding = null;
                return PopupUIGroup.LRB2(n, () => $"Keybinding for \"{b.Purpose}\"",
                    r => new UIColumn(r, new UINode(() => {
                            var curr = $"Current binding: {b.Sources[index]?.Description ?? "(No binding)"}";
                            if (newTempBinding is { } nb)
                                curr += $"\nNew binding: {nb.ValueOrNull?.Description ?? "(No binding)"}";
                            return curr;
                        }) { Prefab = GameManagement.References.uxmlDefaults.PureTextNode, Passthrough = true }
                            .With(fontControlsClass),
                        new KeyRebindInputNode(LString.Empty, keys => 
                                newTempBinding = keys == null ? 
                                    Maybe<IInspectableInputBinding>.None :
                                    new(SimultaneousInputBinding.FromMany(keys)), mode)
                            .With(noSpacePrefixClass, centerTextClass)),
                    null,
                    new UINode[] {
                        new UIButton("Unassign", UIButton.ButtonType.Confirm, _ => {
                            newTempBinding = Maybe<IInspectableInputBinding>.None;
                            return new UIResult.StayOnNode();
                        }),
                        new UIButton("Confirm", UIButton.ButtonType.Confirm, _ => {
                            if (newTempBinding is { } nb) {
                                b.ChangeBindingAt(index, nb.ValueOrNull);
                                InputSettings.SaveInputConfig();
                            }
                            return new UIResult.ReturnToTargetGroupCaller(n.Group);
                        })
                    }
                );
            }) { OnBuilt = n => n.HTML.style.width = 30f.Percent() }.With(small1Class, fontControlsClass);
        }
        UIGroup[] MakeBindings(RebindableInputBinding[] src, KeyRebindInputNode.Mode mode) => 
            src.Select(b => (UIGroup)new UIRow(
                controlsSpace.Construct(Prefabs.UIScreenRow),
                new PassthroughNode(() => b.Purpose) {
                    OnBuilt = n => n.HTML.style.width = 40f.Percent()
                },
                NodeForBinding(b, 0, mode), NodeForBinding(b, 1, mode)
            )).ToArray();
        var header = new UIRow(controlsHeader,
            new PassthroughNode("Key") {
                OnBuilt = n => n.HTML.style.width = 40f.Percent()
            },
            new PassthroughNode("Binding A") {
                OnBuilt = n => n.HTML.style.width = 30f.Percent()
            },
            new PassthroughNode("Binding B") {
                OnBuilt = n => n.HTML.style.width = 30f.Percent()
            });
        var kbBindingsLead = new UIRow(controlsSpace.Construct(Prefabs.UIScreenRow), 
            new PassthroughNode("Keyboard Bindings").With(large1Class));
        var kbBindings = MakeBindings(InputSettings.i.KBMBindings, KeyRebindInputNode.Mode.KBM);
        var cBindingsLead = new UIRow(controlsSpace.Construct(Prefabs.UIScreenRow), 
            new PassthroughNode("Controller Bindings").With(large1Class));
        var cBindings = MakeBindings(InputSettings.i.ControllerBindings, KeyRebindInputNode.Mode.Controller);
        
        var controlsGroup = new UINode("<cspace=16>CONTROLS</cspace>") {
            Prefab = GameManagement.UXMLPrefabs.HeaderNode,
            ShowHideGroup = new VGroup(
                kbBindings.Prepend(kbBindingsLead).Concat(
                        cBindings.Prepend(cBindingsLead))
                    .Prepend(header)
                    .ToArray()
            ) {
                EntryNodeOverride = kbBindings[0].Nodes[2], 
                EntryNodeBottomOverride = cBindings[^1].Nodes[2]
            }
        };
        
        s.SetFirst(new UIRow(new UIRenderExplicit(s, ve => ve.Q("HeaderRow")), new[] {
            new UINode("<cspace=16>GAME</cspace>") {
                Prefab = GameManagement.UXMLPrefabs.HeaderNode,
                //Using UIRenderConstructed allows making different "screens" for each options page
                ShowHideGroup = new UIColumn(new UIRenderConstructed(s, Prefabs.UIScreenColumn, 
                        (_, ve) => ve.style.maxWidth = new Length(60, LengthUnit.Percent)), 
                    allowStaticOptions ?
                            new OptionNodeLR<string?>(main_lang, l => {
                                SaveData.s.TextLocale.OnNext(l);
                                SaveData.AssignSettingsChanges();
                        }, new[] {
                            (LText.Make("English"), Locales.EN),
                            (LText.Make("日本語"), Locales.JP)
                        }, SaveData.s.TextLocale) :
                        null,
                    allowStaticOptions ?
                        new OptionNodeLR<bool>(smoothing, b => SaveData.s.AllowInputLinearization = b, OnOffOption,
                            SaveData.s.AllowInputLinearization) :
                        null,
                    new OptionNodeLR<float>(screenshake, b => SaveData.s.Screenshake = b, new(LString, float)[] {
                            ("Off", 0),
                            ("x0.5", 0.5f),
                            ("x1", 1f),
                            ("x1.5", 1.5f),
                            ("x2", 2f)
                        }, SaveData.s.Screenshake),
                    new OptionNodeLR<bool>(hitbox, b => SaveData.s.UnfocusedHitbox = b, new[] {
                        (hitbox_always, true),
                        (hitbox_focus, false)
                    }, SaveData.s.UnfocusedHitbox),
                    new OptionNodeLR<bool>(backgrounds, b => {
                            SaveData.s.Backgrounds = b;
                            SaveData.UpdateResolution();
                        }, OnOffOption,
                        SaveData.s.Backgrounds),
                    allowStaticOptions ?
                        new OptionNodeLR<float>(dialogue_speed, ds => SaveData.VNSettings.TextSpeed = ds, new(LString, float)[] {
                            ("x2", 2f),
                            ("x1.5", 1.5f),
                            ("x1", 1f),
                            ("x0.75", 0.75f),
                            ("x0.5", 0.5f),
                        }, SaveData.VNSettings.TextSpeed) :
                        null,
                    new OptionNodeLR<float>(dialogue_opacity, x => SaveData.s.VNDialogueOpacity = x, 11.Range().Select(x =>
                        (LText.Make($"{x * 10}"), x / 10f)).ToArray(), SaveData.s.VNDialogueOpacity),
                    new OptionNodeLR<bool>(dialogue_skip, x => SaveData.s.VNOnlyFastforwardReadText = x, new[] {
                        (dialogue_skip_read, true),
                        (dialogue_skip_all, false)
                    }, SaveData.s.VNOnlyFastforwardReadText)
                )
            },
            new UINode("<cspace=16>GRAPHICS</cspace>") {
                Prefab = GameManagement.UXMLPrefabs.HeaderNode,
                ShowHideGroup = new UIColumn(new UIRenderConstructed(s, Prefabs.UIScreenColumn, 
                    (_, ve) => ve.style.maxWidth = new Length(60, LengthUnit.Percent)), 
                    new OptionNodeLR<bool>(shaders, yn => SaveData.s.Shaders = yn, new[] {
                        (shaders_low, false),
                        (shaders_high, true)
                    }, SaveData.s.Shaders),
                    new OptionNodeLR<(int, int)>(resolution, b => SaveData.UpdateResolution(b), new (LString, (int, int))[] {
                        ("3840x2160", (3840, 2160)),
                        ("2560x1440", (2560, 1440)),
                        ("1920x1080", (1920, 1080)),
                        ("1600x900", (1600, 900)),
                        ("1280x720", (1280, 720)),
                        ("848x477", (848, 477)),
                        ("640x360", (640, 360))
                    }, SaveData.s.Resolution),
                    new OptionNodeLR<FullScreenMode>(fullscreen, SaveData.UpdateFullscreen, new[] {
                        (fullscreen_exclusive, FullScreenMode.ExclusiveFullScreen),
                        (fullscreen_borderless, FullScreenMode.FullScreenWindow),
                        (fullscreen_window, FullScreenMode.Windowed),
                    }, SaveData.s.Fullscreen),
                    new OptionNodeLR<int>(vsync, v => SaveData.s.Vsync = v, new[] {
                        (generic_off, 0),
                        (generic_on, 1),
                        //(vsync_double, 2)
                    }, SaveData.s.Vsync)
#if !WEBGL
                    , new OptionNodeLR<bool>(LocalizedStrings.UI.renderer, b => SaveData.s.LegacyRenderer = b, new[] {
                        (renderer_legacy, true),
                        (renderer_normal, false)
                    }, SaveData.s.LegacyRenderer)
#endif
                )
            },
            new UINode("<cspace=16>SOUND</cspace>") {
                Prefab = GameManagement.UXMLPrefabs.HeaderNode,
                ShowHideGroup = new UIColumn(new UIRenderConstructed(s, Prefabs.UIScreenColumn, 
                    (_, ve) => ve.style.maxWidth = new Length(60, LengthUnit.Percent)),
                    new OptionNodeLR<float>(master_volume, SaveData.s.MasterVolume.OnNext, 21.Range().Select(x =>
                        ((LString)$"{x * 10}", x / 10f)).ToArray(), SaveData.s.MasterVolume),
                    new OptionNodeLR<float>(bgm_volume, SaveData.s._BGMVolume.OnNext, 21.Range().Select(x =>
                        ((LString)$"{x * 10}", x / 10f)).ToArray(), SaveData.s._BGMVolume),
                    new OptionNodeLR<float>(sfx_volume, SaveData.s._SEVolume.OnNext, 21.Range().Select(x =>
                        ((LString)$"{x * 10}", x / 10f)).ToArray(), SaveData.s._SEVolume),
                    new OptionNodeLR<float>("Dialogue Typing Volume", SaveData.s._VNTypingSoundVolume.OnNext, 21.Range().Select(x =>
                        ((LString)$"{x * 10}", x / 10f)).ToArray(), SaveData.s._VNTypingSoundVolume)
                )
            }, controlsGroup
        }));
        return s;
    }

    public static UIScreen SaveLoadVNScreen(this UIController m, Func<SerializedSave, bool>? loader, Func<int, SerializedSave>? saver, bool loadIsDangerous=true) {
        int perPage = 8;
        UINode CreateSaveLoadEntry(int i) {
            return new FuncNode(LString.Empty, n => {
                var ind = n.Group.Nodes.IndexOf(n);
                var save = SaveData.v.Saves.TryGetValue(i, out var _s) ? _s : null;
                if (saver == null && (loader == null || save == null))
                    return new UIResult.StayOnNode(true);
                return PopupUIGroup.LRB2(n, () => saveload_header,
                    r => new UIColumn(r,
                            new UINode(saveload_what_do_ls(i + 1))
                                { Prefab = Prefabs.PureTextNode })
                        { Interactable = false },
                    null, new UINode?[] {
                        (save == null) ?
                            null :
                            UIButton.Delete(() => SaveData.v.TryDeleteSave(save),
                                () => new UIResult.GoToNode(n.Group, ind)),
                        (save == null || loader == null) ?
                            null :
                            UIButton.Load(() => loader(save), new UIResult.StayOnNode(), loadIsDangerous),
                        (saver == null) ?
                            null :
                            new UIButton(save == null ? generic_save : generic_overwrite,
                                save == null ? UIButton.ButtonType.Confirm : UIButton.ButtonType.Danger,
                                _ => {
                                    SaveData.v.SaveNewSave(saver(i));
                                    return new UIResult.ReturnToTargetGroupCaller(n);
                                })
                    });
            }) {
                Prefab = Prefabs.SaveLoadNode,
                InlineStyle = (_, n) => {
                    var title = n.HTML.Q<Label>("Title");
                    title.text = $"Save #{i + 1}";
                    var desc = n.HTML.Q<Label>("Description");
                    var bg = n.HTML.Q("SS");
                    if (SaveData.v.Saves.TryGetValue(i, out var save)) {
                        title.RemoveFromClassList("saveentry-title-unset");
                        desc.text = save.Description;
                        bg.style.backgroundImage = save.Image.Texture;
                    } else {
                        title.AddToClassList("saveentry-title-unset");
                        desc.text = "";
                        bg.style.backgroundImage = UXMLPrefabs.defaultSaveLoadBG;
                    }
                }
            };
        }

        var s = new UIScreen(m, "SAVE/LOAD") {
            Builder = (s, ve) => {
                s.Header.style.marginRight = 100;
                s.Margin.SetLRMargin(360, 360);
                var c1 = ve.AddColumn();
                var c2 = ve.AddColumn();
                c1.style.justifyContent = c2.style.justifyContent = Justify.SpaceBetween;
            }
        };
        
        UINode CreatePage(int p) {
            var c1 = new UIColumn(s, null, (p * perPage, p * perPage + perPage / 2).Range()
                .Select(CreateSaveLoadEntry));
            return new UINode($"{p + 1}") {
                Prefab = Prefabs.HeaderNode,
                OnBuilt = n => {
                    n.Label!.style.unityTextAlign = TextAnchor.MiddleCenter;
                    n.Label.style.fontSize = 100;
                    n.BodyHTML.SetPadding(0, 25, 0, 25);
                },
                ShowHideGroup = new HGroup(
                    c1,
                    new UIColumn(s, new UIRenderColumn(s, 1), (p * perPage + perPage / 2, (p + 1) * perPage)
                        .Range().Select(CreateSaveLoadEntry))
                ) { EntryNodeOverride = c1.EntryNode}
            };
        }

        var pages = 9.Range().Select(CreatePage).ToArray();
        s.SetFirst(new UIRow(new UIRenderExplicit(s, ve => ve.Q("HeaderRow")), pages));
        return s;
    }
    public static UIScreen ReplayScreen(this UIController m, UIScreen gameDetails) {
        var s = new UIScreen(m, "REPLAYS") {
            Builder = (_, ve) => ve.AddScrollColumn()
        };
        s.SetFirst(new UIColumn(s, null) {
            LazyNodes = () => SaveData.p.ReplayData.Select(rep => {
                return new FuncNode(rep.metadata.Record.AsDisplay(true, true), n => {
                    var ind = n.Group.Nodes.IndexOf(n);
                    return PopupUIGroup.LRB2(n, () => replay_window,
                        r => new UIColumn(r,
                                new UINode(replay_what_do_ls(rep.metadata.Record.CustomName))
                                    { Prefab = Prefabs.PureTextNode })
                            { Interactable = false },
                        null, new UINode[] {
                            UIButton.Delete(() => {
                                if (SaveData.p.TryDeleteReplay(rep)) {
                                    n.Remove();
                                    return true;
                                } else return false;
                            }, () => new UIResult.GoToNode(n.Group, ind)),
                            new UIButton(view_details, UIButton.ButtonType.Confirm, _ => 
                                n.ReturnGroup.Then(CreateGameResultsView(rep.metadata.Record, gameDetails))),
                            new UIButton(replay_view, UIButton.ButtonType.Confirm, _ => {
                                s.Controller.ConfirmCache();
                                return new UIResult.StayOnNode(!InstanceRequest.ViewReplay(rep));
                            })
                        });
                }) {
                    CacheOnEnter = true
                }.With(monospaceClass, small2Class, centerTextClass);
            })
        });
        return s;
    }

    public static UIScreen RecordsScreen(this UIController m, UIScreen replayScreen, UIScreen detailsScreen,
        SMAnalysis.AnalyzedCampaign[] campaigns, SMAnalysis.AnalyzedDayCampaign? days = null) {
        var screen = new UIScreen(m, "RECORDS") {Builder = (s, ve) => {
            s.Margin.SetLRMargin(600, 600);
            var container = ve.AddColumn();
            var opts = container.AddColumn();
            opts.style.flexGrow = opts.style.flexShrink = 0;
            opts.style.height = 30f.Percent();
            opts.style.width = 100f.Percent();
            opts.style.marginBottom = 40;
            var scores = container.AddScrollColumn();
            scores.SetLRMargin(0, 60);
            scores.style.width = 100f.Percent();
        }};
        if (campaigns.Length == 0 || campaigns[0].bosses.Length == 0) {
            _ = new UIColumn(screen, null, new UINode(scores_nocampaign));
            return screen;
        }
        var mode = InstanceMode.CAMPAIGN;
        int cmpIndex;
        string _campaign;
        string _boss;
        int _bphase;
        int _stage;
        int _sphase;
        bool Matches(LowInstanceRequestKey key) => mode switch {
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
        var scoreNodes = SaveData.r.FinishedGames.Values
            //If the user doesn't enter a name on the replay screen, the score won't show up, but it will still be recorded internally
            .Where(g => !string.IsNullOrWhiteSpace(g.CustomNameOrPartial) && g.Score > 0)
            .OrderByDescending(g => g.Score).Select(g =>
                //Don't need to show the request (eg. Yukari (Ex) p3) because it's shown by the option nodes above this
                new FuncNode(() => g.AsDisplay(true, false), n => PopupUIGroup.LRB2(
                        n, () => record_header, 
                        r => new UIColumn(r,new UINode(record_what_do(g.CustomNameOrPartial)) 
                            { Prefab = Prefabs.PureTextNode} ) { Interactable = false },
                        null, new UINode[] {
                            new UIButton(view_details, UIButton.ButtonType.Confirm, _ =>
                                n.ReturnGroup.Then(CreateGameResultsView(g, detailsScreen))),
                            new UIButton(record_view_replay, UIButton.ButtonType.Confirm, _ => {
                                foreach (var (ir, replay) in SaveData.p.ReplayData.Enumerate())
                                    if (replay.metadata.Record.Uuid == g.Uuid)
                                        return  n.ReturnGroup.Then(new UIResult.GoToNode(replayScreen.Groups[0], ir));
                                return new UIResult.StayOnNode(true);
                            }) { EnabledIf = () =>SaveData.p.ReplayData.Any(rep => rep.metadata.Record.Uuid == g.Uuid) }
                        }
                    )) {
                    VisibleIf = () => Matches(g.RequestKey)
                }.With(monospaceClass, small2Class, centerTextClass));
        bool IsBossOrChallenge() => mode is InstanceMode.BOSS_PRACTICE or InstanceMode.SCENE_CHALLENGE;
        bool IsStage() => mode == InstanceMode.STAGE_PRACTICE;
        var optnodes = new UINode[] {
            new OptionNodeLR<InstanceMode>(practice_type, i => mode = i, new[] {
                (practice_m_campaign, InstanceMode.CAMPAIGN),
                (practice_m_boss, InstanceMode.BOSS_PRACTICE),
                days == null ? ((LString, InstanceMode)?) null : (practice_m_scene, InstanceMode.SCENE_CHALLENGE),
                (practice_m_stage, InstanceMode.STAGE_PRACTICE)
            }.FilterNone().ToArray(), mode),
            new OptionNodeLR<int>(practice_campaign, AssignCampaign,
                campaigns.Select((c, i) => ((LString)c.campaign.shortTitle, i)).ToArray(), cmpIndex),
            new OptionNodeLR<string>(practice_m_whichboss, AssignBoss, () =>
                    IsBossOrChallenge() ?
                        campaigns[cmpIndex].bosses.Select(b => (b.boss.BossPracticeName, b.boss.key)).ToArray() :
                        new (LString, string)[] {("", "")} //required to avoid errors with the option node
                , ""){ VisibleIf = IsBossOrChallenge },
            new OptionNodeLR<int>(practice_m_whichstage, AssignStage, () =>
                    IsStage() ?
                        campaigns[cmpIndex].stages.Select((s, i) => ((LString)s.stage.stageNumber, i)).ToArray() :
                        new (LString, int)[] {("", 0)} //required to avoid errors with the option node
                , 0){ VisibleIf = IsStage },
            new OptionNodeLR<int>(practice_m_whichphase, AssignBossPhase, () =>
                    IsBossOrChallenge() ?
                        campaigns[cmpIndex].bossKeyMap[_boss].Phases.Select(
                            //p.index is used as request key
                            (p, i) => ((LString)$"{i + 1}. {p.Title}", p.index)).ToArray() :
                        new (LString, int)[] {("", 0)}, 0) {
                    OnBuilt = n => n.NodeHTML.Q("ValueContainer").style.width = new StyleLength(new Length(80, LengthUnit.Percent)),
                    VisibleIf = IsBossOrChallenge
                },
            new OptionNodeLR<int>(practice_m_whichphase, AssignStagePhase, () =>
                    IsStage() ?
                        campaigns[cmpIndex].stages[_stage].Phases.Select(
                            p => (p.Title, p.index)).Prepend((practice_fullstage, 1)).ToArray() :
                        new (LString, int)[] {("", 0)}, 0) {
                    VisibleIf = IsStage
                }
        };
        screen.SetFirst(new VGroup(
            new UIColumn(screen.ColumnRender(1), optnodes),
            new UIColumn(screen.ColumnRender(2), scoreNodes)) {
            EntryNodeOverride = optnodes[0]
        });
        return screen;
    }
    
    public static UIScreen CreatePlayerScreen(this UIController m, SMAnalysis.AnalyzedCampaign c, 
        BehaviorEntity? demoSetup, GameObject? demoPlayerPrefab, Transform shotDisplayContainer, Func<TeamConfig, bool> continuation) {
        foreach (var sc in c.campaign.players
            .SelectMany(p => p.shots2)
            .Select(s2 => s2.shot)) {
            if (sc.prefab != null)
                foreach (var fo in sc.prefab.GetComponentsInChildren<FireOption>())
                    fo.Preload();
        }
        PlayerController? demoPlayer = null;
        Cancellable? demoCT = null;
        OptionNodeLR<ShipConfig> playerSelect = null!;
        OptionNodeLR<IAbilityCfg> supportSelect = null!;
        OptionNodeLR<ShotConfig> shotSelect = null!;
        OptionNodeLR<Subshot> subshotSelect = null!;

        var team = new TeamConfig(0, Subshot.TYPE_D, null,
            c.campaign.players
                .SelectMany(p => p.shots2
                    .Select(s => (p, s.shot)))
                .ToArray());
        var smeta = new SharedInstanceMetadata(team, new DifficultySettings(FixedDifficulty.Normal));
        
        void CleanupDemo() {
            Logs.Log("Cleaning up demo");
            //If you don't do this, lingering references to playercontroller in the fctx of
            // fired lasers can become problematic
            BulletManager.ClearAllBullets();
            if (demoPlayer != null) {
                demoPlayer.InvokeCull();
                demoPlayer = null;
            }
            demoCT?.Cancel();
            GameManagement.DeactivateInstance();
        }
        void UpdateDemo() {
            if (demoSetup == null || demoPlayerPrefab == null) return;
            GameManagement.DeactivateInstance();
            var effShot = shotSelect.Value.GetSubshot(subshotSelect.Value);
            ReplayActor r;
            if (effShot.demoReplay != null) {
                r = Replayer.BeginReplaying(new Replayer.ReplayerConfig(
                    Replayer.ReplayerConfig.FinishMethod.REPEAT, 
                    effShot.demoReplay.Frames,
                    () => demoPlayer!.transform.position = new Vector2(0, -3)
                ));
                demoCT?.Cancel();
                demoCT = new Cancellable();
                if (effShot.demoSetupSM != null) {
                    StateMachineManager.FromText(effShot.demoSetupSM)?.Start(new SMHandoff(demoSetup, demoCT));
                }
            } else {
                r = Replayer.BeginReplaying(new Replayer.ReplayerConfig(
                    Replayer.ReplayerConfig.FinishMethod.REPEAT,
                    () => new []{new InputManager.FrameInput(0, 0, false, false, false, false, false, false, false)}
                ));
            }
            GameManagement.NewInstance(InstanceMode.NULL, InstanceFeatures.ShotDemoFeatures, 
                new InstanceRequest((_, __) => { }, smeta, new CampaignRequest(c!)), r);
            if (demoPlayer == null) {
                demoPlayer = UnityEngine.Object.Instantiate(demoPlayerPrefab).GetComponent<PlayerController>();
            }
            demoPlayer.UpdateTeam((playerSelect.Value, shotSelect.Value), subshotSelect.Value, true);
            demoPlayer.transform.position = new Vector2(0, -3);
        }
        
        (ShipConfig player, FancyShotDisplay display)[] displays = c.campaign.players.Select(p => {
            var display = UnityEngine.Object.Instantiate(p.shotDisplay, shotDisplayContainer).GetComponent<FancyShotDisplay>();
            display.Show(false);
            return (p, display);
        }).ToArray();

        void ShowShot(ShipConfig p, ShotConfig s, Subshot sub, IAbilityCfg support, bool first) {
            if (!first) UpdateDemo();
            var index = displays.IndexOf(sd => sd.player == p);
            displays[index].display.SetShot(p, s, sub, support);
            foreach (var (i, (_, display)) in displays.Enumerate()) {
                //Only show the selected player on entry so the others don't randomly appear on screen during swipe
                display.Show(!first || i == index);
                display.SetRelative(i, index, first);
            };
        }

        void _ShowShot(bool first = false) {
            ShowShot(playerSelect.Value, shotSelect.Value, subshotSelect.Value, supportSelect.Value, first);
        }
        
        playerSelect = new OptionNodeLR<ShipConfig>(LString.Empty, _ => _ShowShot(),
            c.campaign.players.Select(p => (p.ShortTitle, p)).ToArray(), c.campaign.players[0]);

        supportSelect = new OptionNodeLR<IAbilityCfg>(LString.Empty, _ => _ShowShot(),
            () => playerSelect.Value.supports.Select(s => 
                ((LString)s.ordinal, (IAbilityCfg)s.ability)).ToArray(), 
            playerSelect.Value.supports[0].ability);
        shotSelect = new OptionNodeLR<ShotConfig>(LString.Empty, _ => _ShowShot(), () =>
                playerSelect.Value.shots2.Select(s => ((LString)(s.shot.isMultiShot ? 
                        shotsel_multi(s.ordinal) : 
                        shotsel_type(s.ordinal)), s.shot)).ToArray(),
            playerSelect.Value.shots2[0].shot);
        subshotSelect = new OptionNodeLR<Subshot>(LString.Empty, _ => _ShowShot(),
            EnumHelpers2.Subshots.Select(x => (shotsel_variant_ls(x.Describe()), x)).ToArray(), Subshot.TYPE_D) 
            {VisibleIf = () => shotSelect.Value.isMultiShot};
        var screen = new UIScreen(m, null, UIScreen.Display.Unlined) {
            Builder = (s, ve) => {
                s.Margin.style.marginLeft = 160;
                var g = ve.AddColumn();
                g.style.maxWidth = new Length(25, LengthUnit.Percent);
                g.style.paddingTop = 720;
            },
            OnEnterStart = _ => _ShowShot(true),
            OnEnterEnd = UpdateDemo,
            OnExitStart = () => {
                CleanupDemo();
                foreach (var (player, display) in displays) {
                    if (player != playerSelect.Value) display.Show(false);
                };
            },
        };
        _ = new UIColumn(screen, null,
            new PassthroughNode(shotsel_player).With(centerTextClass),
            playerSelect.With(optionNoKeyClass),
            new PassthroughNode(LString.Empty),
            new PassthroughNode(shotsel_shot).With(centerTextClass),
            shotSelect.With(optionNoKeyClass),
            subshotSelect.With(optionNoKeyClass),
            new PassthroughNode(LString.Empty),
            new PassthroughNode(shotsel_support).With(centerTextClass),
            supportSelect.With(optionNoKeyClass),
            new PassthroughNode(LString.Empty),
            new FuncNode(play_game, () => continuation(new TeamConfig(0, subshotSelect.Value,
                supportSelect.Value,
                (playerSelect.Value, shotSelect.Value)))).With(centerTextClass)
            //new UINode(() => shotSelect.Value.title).SetAlwaysVisible().FixDepth(1),
            //new UINode(() => shotSelect.Value.description)
            //    .With(shotDescrClass).With(smallClass)
            //    .SetAlwaysVisible().FixDepth(1))
        ) { EntryNodeOverride = playerSelect };
        return screen;
    }

    public static UIScreen CustomDifficultyScreen(this UIController menu, Func<DifficultySettings, UIResult> dfcCont) {
        var screen = new UIScreen(menu, "CUSTOM DIFFICULTY SETTINGS") {Builder = (_, ve) => {
            var g1 = ve.AddScrollColumn();
            g1.style.flexGrow = 2.4f;
            g1.style.paddingRight = 120;
            ve.AddScrollColumn().style.flexGrow = 2;
        }};
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
            return ((LString)($"{prefix}{offset}%"), x);
        }).ToArray();
        (LString, bool)[] yesNo = {(generic_on, true), (generic_off, false)};
        IEnumerable<(LString, double)> AddPlus(IEnumerable<double> arr) => arr.Select(x => {
            var prefix = (x >= 0) ? "+" : "";
            return ((LString)($"{prefix}{x}"), x);
        });
        var descCol = screen.ColumnRender(1);
        UINode MakeOption<T>(LString title, IEnumerable<(LString, T)> options, Func<T> deflt, Action<T> apply,
            LString description) {
            var node = new OptionNodeLR<T>(title, apply, options.ToArray(), deflt()) {
                ShowHideGroup = new UIColumn(descCol, new UINode(LString.Format("\n\n{0}", description))) {
                    Interactable = false
                },
                OnBuilt = n => {
                    n.NodeHTML.style.paddingLeft = 20;
                    n.NodeHTML.style.paddingRight = 20;
                },
                OverrideClasses = new() { small1Class }
            };
            load_cbs.Add(() => node.SetIndexFromVal(deflt()));
            return node;
        }
        UINode MakePctOption(LString title, Func<double> deflt, Action<double> apply, LString description)
            => MakeOption(title, pctMods, deflt, apply, description);
        UINode MakeOnOffOption(LString title, Func<bool> deflt, Action<bool> apply, LString description)
            => MakeOption(title, yesNo, deflt, apply, description);
        UINode MakeOptionAuto<T>(LString title, IEnumerable<T> options, Func<T> deflt, Action<T> apply, LString description)
            => MakeOption(title, options.Select(x => ((LString)(x!.ToString()), x)), deflt, apply, description);

        var saved = SaveData.s.DifficultySettings;

        
        
        UINode MakeSaveLoadDFCNode((string name, DifficultySettings settings) s) =>
            new FuncNode(() => (LString)(s.name),
                n => {
                    var ind = n.Group.Nodes.IndexOf(n);
                    return PopupUIGroup.LRB2(n, () => setting,
                        r => new UIColumn(r, new UINode(setting_what_do_ls(s.name)) {Prefab = Prefabs.PureTextNode}) 
                            { Interactable = false },
                        null, new UINode[] {
                            UIButton.Delete(() => {
                                if (SaveData.s.RemoveDifficultySettings(s)) {
                                    n.Remove();
                                    return true;
                                } else return false;
                            }, () => new UIResult.GoToNode(n.Group, ind)),
                            UIButton.Load(() => {
                                SetNewDFC(s.settings);
                                return true;
                            }, n.ReturnGroup),
                        }
                    );
                });
        
        var optSliderHelper = new PassthroughNode(() =>
            desc_effective_ls(effective, DifficultySettings.FancifySlider(dfc.customValueSlider)));
        dfc.respawnOnDeath = false;
        screen.SetFirst(new UIColumn(screen, null,
            MakeOption(scaling, (DifficultySettings.MIN_SLIDER, DifficultySettings.MAX_SLIDER + 1).Range()
                .Select(x => ((LString)($"{x}"), x)), () => dfc.customValueSlider, dfc.SetCustomDifficulty,
                desc_scaling),
            optSliderHelper.With(small2Class),
            MakeOptionAuto(suicide, new[] {0, 1, 3, 5, 7}, () => dfc.numSuicideBullets,
                x => dfc.numSuicideBullets = x, desc_suicide),
            MakePctOption(p_dmg, () => dfc.playerDamageMod, x => dfc.playerDamageMod = x, desc_p_dmg),
            MakePctOption(boss_hp, () => dfc.bossHPMod, x => dfc.bossHPMod = x, desc_boss_hp),
            MakeOnOffOption(respawn, () => dfc.respawnOnDeath.Value, x => dfc.respawnOnDeath = x, desc_respawn),
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
            MakeOption(lives, (1, 14).Range().Select(x => ((LString)($"{x}"), (int?) x)).Prepend((generic_default, null)),
                () => dfc.startingLives, x => dfc.startingLives = x, desc_lives),
            MakeOption(poc, AddPlus(new[] {
                    //can't use addition to generate these because -6 + 0.4 =/= -5.6...
                    -6, -5.6, -5.2, -4.8, -4.4, -4, -3.6, -3.2, -2.8, -2.4, -2, -1.6, -1.2, -0.8, -0.4,
                    0, 0.4, 0.8, 1.2, 1.6, 2
                }), () => dfc.pocOffset, x => dfc.pocOffset = x, desc_poc),
            //new PassthroughNode(""),
            new UINode(to_select) { OnConfirm = _ => dfcCont(dfc) } ,
            new UINode(manage_setting) {
                ShowHideGroup = new UIColumn(descCol, 
                    saved.Select(MakeSaveLoadDFCNode)
                        .Prepend(new FuncNode(() => create_setting, 
                            n => {
                                var settingNameEntry = new TextInputNode(LString.Empty);
                                return PopupUIGroup.LRB2(n, () => create_setting,
                                    r => new UIColumn(r, new UINode(new_setting_name) {
                                        Prefab = Prefabs.PureTextNode, Passthrough = true
                                    }, settingNameEntry),
                                    null, new UINode[] {
                                        UIButton.Save(() => {
                                            SaveData.s.AddDifficultySettings(settingNameEntry.DataWIP, dfc);
                                            n.Group.AddNodeDynamic(MakeSaveLoadDFCNode(saved.Last()));
                                            return true;
                                        }, n.ReturnGroup), 
                                    });
                            }) {
                            InlineStyle = (_, n) => n.NodeHTML.style.marginBottom = 120
                        }))
            }
        ));
        return screen;
    }
    
    public static UIScreen StatisticsScreen(this UIController menu, IDanmakuGameDef game, IEnumerable<InstanceRecord> allGames, 
        SMAnalysis.AnalyzedCampaign[] campaigns) {
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
                "{0}: {1}", 
                (LString)(AsPct(bpr.ratio)),
                bpr.card.phase.Title
            );
        }

        var optNodes = new UINode[] {
            new OptionNodeLR<int?>(practice_campaign, AssignCampaign,
                campaigns
                    .Select((c, i) => ((LString)(c.campaign.shortTitle), (int?)i))
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
                game.AllShips
                    .Select(x => (x.ShortTitle, (ShipConfig?)x))
                    .Prepend((stats_allplayers, null)).ToArray(), playerSwitch),
            new OptionNodeLR<(ShipConfig, ShotConfig)?>(stats_selshot, x => {
                    shotSwitch = x;
                    UpdateStats();
                },
                game.AllShips
                    .SelectMany(p => p.shots2
                        .Select(os => (ShotConfig.PlayerShotDescription(p, os.shot),
                            ((ShipConfig, ShotConfig)?)(p, os.shot))))
                    .Prepend((stats_allshots, null)).ToArray(), shotSwitch),
        };
        var statsNodes = new UINode[] {
            new TwoLabelUINode(stats_allruns, () => $"{stats.TotalRuns}"),
            new TwoLabelUINode(stats_complete, () => $"{stats.CompletedRuns}"),
            new TwoLabelUINode(stats_1cc, () => $"{stats.OneCCRuns}"),
            new TwoLabelUINode(stats_deaths, () => $"{stats.TotalDeaths}"),
            new TwoLabelUINode(stats_totaltime, () => stats.TotalFrames.FramesToTime()),
            new TwoLabelUINode(stats_avgtime, () => stats.AvgFrames.FramesToTime()),
            new TwoLabelUINode(stats_favday, () => 
                stats.TotalRuns == 0 ? generic_na :
                (LString)($"{stats.FavoriteDay.Item1} ({stats.FavoriteDay.Item2.Length})")),
            new TwoLabelUINode(stats_favplayer, () => 
                stats.TotalRuns == 0 ? generic_na :
                    LString.Format(
                "{0} ({1})", stats.FavoriteShip.Item1.ShortTitle, (LString)($"{stats.FavoriteShip.Item2.Length}")
            )),
            new TwoLabelUINode(stats_favshot, () => {
                if (stats.TotalRuns == 0) return generic_na;
                var ((pc, sc), recs) = stats.FavoriteShot;
                return LString.Format("{0} ({1})", 
                    ShotConfig.PlayerShotDescription(pc, sc),
                    $"{recs.Length}"
                );
            }),
            new TwoLabelUINode(stats_highestscore, () => 
                stats.TotalRuns == 0 ? generic_na : $"{stats.MaxScore}"),
            new TwoLabelUINode(stats_capturerate, () => AsPct(stats.CaptureRate)),
            new TwoLabelUINode(stats_bestcard, () => 
                !stats.HasSpellHist ? generic_na : ShowCard(stats.BestCapture)),
            new TwoLabelUINode(stats_worstcard, () => 
                !stats.HasSpellHist ? generic_na : ShowCard(stats.WorstCapture))
        }.Select(x => x.With(small1Class));
        
        var screen = new UIScreen(menu, "STATISTICS") {Builder = (s, ve) => {
            s.Margin.SetLRMargin(720, 720);
            var container = ve.AddColumn();
            var opts = container.AddColumn();
            opts.style.flexGrow = opts.style.flexShrink = 0;
            opts.style.height = 30f.Percent();
            opts.style.width = 100f.Percent();
            opts.style.marginBottom = 40;
            var scores = container.AddScrollColumn();
            scores.SetLRMargin(0, 60);
            scores.style.width = 100f.Percent();
        }};
        screen.SetFirst(new VGroup(
                new UIColumn(screen.ColumnRender(1), optNodes),
                new UIColumn(screen.ColumnRender(2), statsNodes)
            ) {
            EntryNodeOverride = optNodes[0]
        });
        return screen;
    }

    public static UIScreen AchievementsScreen(this UIController menu, VisualTreeAsset node, 
        AchievementManager acvs) {
        var screen = new UIScreen(menu, "ACHIEVEMENTS") {Builder = (s, ve) => {
            s.Margin.SetLRMargin(600, 720);
            ve.AddScrollColumn();
        }};
        _ = new UIColumn(screen, null, acvs.SortedAchievements.Select(a =>
                new UINode(a.Title) {
                        Prefab = node,
                        OnBuilt = n => {
                            n.NodeHTML.style.paddingLeft = 20;
                            n.NodeHTML.style.paddingRight = 20;
                        },
                        InlineStyle = (_, n) => {
                            n.NodeHTML.Q<Label>("Description").text = a.VisibleDescription;
                            n.NodeHTML.AddToClassList(CheckmarkClass(a.Completed));
                        }
                }
            ).ToArray()
        );
        return screen;
    }

    public static UIScreen MusicRoomScreen(this UIController menu, IEnumerable<IAudioTrackInfo> musics) {
        var screen = new UIScreen(menu, "MUSIC ROOM") {Builder = (_, ve) => {
            ve.AddScrollColumn().style.flexGrow = 1.7f;
            ve.AddColumn();
        }};
        var descCol = new UIRenderColumn(screen, 1);
        screen.SetFirst(new UIColumn(screen, null, musics.SelectNotNull(m => (m.DisplayInMusicRoom switch {
            true => new FuncNode(string.Format("({0}) {1}", m.TrackPlayLocation, m.Title), 
                () => new UIResult.StayOnNode(ServiceLocator.Find<IAudioTrackService>().InvokeBGM(m) == null)) {
                ShowHideGroup = new UIColumn(descCol, 
                    new UINode(m.MusicRoomDescription).With(small2Class, fontUbuntuClass)) 
                {Interactable = false}
            },
            false => new UINode("????????????????") {
                ShowHideGroup = new UIColumn(descCol, 
                    new UINode("This track is not yet unlocked.").With(small2Class, fontUbuntuClass))
                {Interactable = false}
            },
            _ => null
        })?.With(small1Class))));
        return screen;
    }

    public static (IEnumerable<UINode?> left, IEnumerable<UINode?> right) GameMetadataDisplay(InstanceRecord rec) {
        var stats = new CardHistoryStats(rec.CardHistory);
        IEnumerable<UINode?> reqNodes = rec.RequestKey.Reconstruct() switch {
            BossPracticeRequest br => new TwoLabelUINode?[] {
                new(practice_campaign, br.boss.campaign.campaign.shortTitle),
                new(practice_m_whichboss, br.boss.boss.BossPracticeName),
                new(practice_m_whichphase, br.phase.Title)
            },
            CampaignRequest cr => new TwoLabelUINode?[] {
                new(practice_campaign, cr.campaign.campaign.shortTitle)
            },
            PhaseChallengeRequest pr => new TwoLabelUINode?[] {
                new(practice_campaign, pr.phase.boss.day.campaign.campaign.shortTitle),
                new(practice_m_whichboss, pr.phase.boss.boss.BossPracticeName),
                new(practice_m_whichphase, pr.phase.phase.Title),
                new("Challenge", pr.ChallengeIdx)
            },
            StagePracticeRequest sr => new TwoLabelUINode?[] {
                new(practice_campaign, sr.stage.campaign.campaign.shortTitle),
                new(practice_m_whichstage, sr.stage.stage.stageNumber),
                new(practice_m_whichphase, sr.phase == 1 ? practice_fullstage : sr.stage.Phases.First(p => p.index == sr.phase))
            },
            _ => throw new ArgumentOutOfRangeException()
        };
        var smeta = rec.SharedInstanceMetadata;
        var ship = smeta.team.ships.TryN(0);
        return (reqNodes
            .Prepend(new TwoLabelUINode(practice_type, rec.Mode.Describe()))
            //() => allows updates when a replay name is saved
            .Prepend(new TwoLabelUINode("Name", () => rec.CustomNameOrPartial))
            .Append(new UINode {Passthrough = true}).Concat(new TwoLabelUINode?[] {
                new("Completed?", rec.Completed.ToString()),
                new("Difficulty", rec.Difficulty.Describe()),
                new("Player", ShotConfig.PlayerShotDescription(ship?.ship, ship?.shot)),
                new("Date", rec.Date.SimpleTime())
            }), new TwoLabelUINode?[] {
            new("Score", rec.Score),
            new("Continues Used", rec.ContinuesUsed),
            new("Photos Taken", rec.Photos.Length),
            new("Hits Taken", rec.HitsTaken),
            new("Bombs Used", rec.BombsUsed),
            new("1-UP Items Collected", rec.OneUpItemsCollected),
            new("Game Time", rec.TotalFrames.FramesToTime()),
            new("Bullet Time Time", rec.MeterFrames.FramesToTime()),
            new("Total Cards", stats.TotalCards),
            new("Perfect Card Captures", $"{stats.CardsPerStarCount[^1]}/{stats.TotalCards}"),
            new("Card Captures By Stars", string.Join("/", stats.CardsPerStarCount.Select(x => x.ToString())))
        });
    }

    public static UIResult CreateGameResultsView(InstanceRecord record, UIScreen resultsScreen) {
        var (l, r) = GameMetadataDisplay(record);
        var options = new UINode[] {
            new FuncNode(generic_back, () => new UIResult.ReturnToScreenCaller())
        };
        var details = new LRBGroup(
            new UIColumn(new UIRenderExplicit(resultsScreen, ve => ve.Q("Left")), l),
            new UIColumn(new UIRenderExplicit(resultsScreen, ve => ve.Q("Right")), r),
            new UIRow(new UIRenderExplicit(resultsScreen, ve => ve.Q("Bottom")), options)
                { ExitNodeOverride = options[0] }
        ) { ExitNodeOverride = options[0], EntryNodeOverride = options[0], OnScreenExitEnd = g => g.Destroy() };
        resultsScreen.AddGroup(details);
        return new UIResult.GoToNode(details);
    }

    public static readonly Action<UIScreen, VisualElement> GameResultsScreenBuilder = (s, ve) => {
        s.Margin.SetLRMargin(600, 600);
        var inner = ve.AddColumn();
        var lr = inner.AddRow();
        lr.style.height = 90f.Percent();
        lr.AddColumn().SetPadding(20, 50, 50, 50).name = "Left";
        lr.AddColumn().SetPadding(20, 50, 50, 50).name = "Right";
        var bot = inner.AddNodeRow();
        bot.style.height = 10f.Percent();
        bot.name = "Bottom";
    };

    public static UIScreen PlayerDataScreen(this UIController m, UIScreen records, UIScreen stats, UIScreen? achievements, UIScreen replays) {
        var s = new UIScreen(m, "PLAYER DATA") {
            Builder = (s, ve) => {
                s.Margin.SetLRMargin(960, 960);
                var c = ve.AddColumn();
                c.style.paddingTop = 240;
            }
        };
        Action<UINode> setMargin = n => {
            n.HTML.style.marginBottom = 50;
        };
        s.SetFirst(new UIColumn(s, null,
                new TransferNode(main_scores, records) {
                        EnabledIf = FinishedCampaigns.Any,
                        OnBuilt = setMargin
                    }.With(large2Class),
                new TransferNode(main_stats, stats) {
                            EnabledIf = FinishedCampaigns.Any,
                            OnBuilt = setMargin
                }.With(large2Class),
                achievements == null ? null :
                    new TransferNode(main_achievements, achievements) {
                            OnBuilt = setMargin
                        }.With(large2Class),
                new TransferNode(main_replays, replays) {
                        EnabledIf = () => SaveData.p.ReplayData.Count > 0,
                        OnBuilt = setMargin
                    }.With(large2Class)
        ));
        return s;
    }

    public static UIScreen AllPlayerDataScreens(this UIController m, IDanmakuGameDef game, UIScreen gameDetails, out UIScreen records, out UIScreen stats,
        out UIScreen? achievements, out UIScreen replays, VisualTreeAsset achievementsNodeV) {
        replays = m.ReplayScreen(gameDetails);
        records = m.RecordsScreen(replays, gameDetails, FinishedCampaigns.ToArray());
        achievements = GameManagement.Achievements != null ? 
            m.AchievementsScreen(achievementsNodeV, GameManagement.Achievements) : 
            null;
        stats = m.StatisticsScreen(game, SaveData.r.FinishedCampaignGames, Campaigns);
        return m.PlayerDataScreen(records, stats, achievements, replays);
    }

    public static UIScreen LicenseScreen(this UIController m, NamedTextAsset[] licenses) {
        var screen = new UIScreen(m, null) {Builder = (s, ve) => {
            var container = ve.AddColumn();
            container.name = "VContainer";
            var opts = container.AddNodeRow();
            opts.name = "Options";
            opts.style.flexGrow = opts.style.flexShrink = 0;
            opts.style.height = 8f.Percent();
            opts.style.width = 100f.Percent();
            opts.style.marginBottom = 40;
        }};
        var contRender = new UIRenderExplicit(screen, ve => ve.Q("VContainer"));
        var ls = licenses.Where(l => l.file != null)
            .Select(l => (l.name, MarkdownParser.Parse(l.file.text))).ToArray();
        screen.SetFirst(new UIRow(new UIRenderExplicit(screen, ve => ve.Q("Options")), 
                ls.Select(l => {
                    var render = new UIRenderConstructed(contRender, Prefabs.UIScreenScrollColumn, 
                        (_, ve) => {
                            ve.style.width = 100f.Percent();
                            ve.SetPadding(0, 180, 0, 210);
                        });
                    return new UINode(l.name) {
                        ShowHideGroup = new UIColumn(render),
                        OnBuilt = _ => {
                            foreach (var b in l.Item2) {
                                render.HTML.Q<ScrollView>().Add(RenderMarkdown(b));
                            }
                        }
                    };
                })
            ));
        return screen;
    }

    private static void Stringify(Markdown.TextRun t, VisualElement into, bool breakOnSpace=false) {
        var sb = new StringBuilder();
        var pxPerSpace = 11;
        bool bold = false;
        bool italic = false;
        void CommitString() {
            if (sb.Length == 0) return;
            var ve = Prefabs.MkLineText.CloneTreeWithoutContainer();
            var s = (ve as Label)!.text = sb.ToString();
            sb.Clear();
            int ii = 0;
            for (; ii < s.Length; ++ii) {
                if (!char.IsWhiteSpace(s[^(1 + ii)]))
                    break;
            }
            //No longer required as of 2022; trailing whitespace is no longer pruned
            ve.style.marginRight = pxPerSpace * ii;
            into.Add(ve);
        }
        bool RequiresSubtype(Markdown.TextRun tr) => tr switch {
            Markdown.TextRun.Bold b => RequiresSubtype(b.Bolded),
            Markdown.TextRun.InlineCode => true,
            Markdown.TextRun.Italic i => RequiresSubtype(i.Italicized),
            Markdown.TextRun.Link => true,
            Markdown.TextRun.Sequence sequence => sequence.Pieces.Any(RequiresSubtype),
            _ => false
        };
        //if we render a long sequence of chars that causes a text wrap in one LineFragmentText, then a successive
        // LineFragmentLink/LineFragmentCode will display on a new line instead of to the right of it. This is
        // not resolvable except by preventing text wrapping, which is best done by breaking on spaces.
        breakOnSpace |= RequiresSubtype(t);
        void ApplyFontStyleToVE(VisualElement ve) {
            if (bold && italic)
                ve.style.unityFontStyleAndWeight = FontStyle.BoldAndItalic;
            else if (bold)
                ve.style.unityFontStyleAndWeight = FontStyle.Bold;
            else if (italic)
                ve.style.unityFontStyleAndWeight = FontStyle.Italic;
        }
        // ReSharper disable once VariableHidesOuterVariable
        void _Stringify(Markdown.TextRun t) {
            switch (t) {
                case Markdown.TextRun.Atom atom:
                    if (breakOnSpace) {
                        var split = atom.Text.Split(' ');
                        for (int ii = 0; ii < split.Length - 1; ++ii) {
                            sb.Append(split[ii]);
                            sb.Append(' ');
                            CommitString();
                        }
                        sb.Append(split[^1]);
                    } else
                        sb.Append(atom.Text);
                    break;
                case Markdown.TextRun.Bold b:
                    sb.Append("<b>");
                    bold = true;
                    _Stringify(b.Bolded);
                    bold = false;
                    sb.Append("</b>");
                    break;
                case Markdown.TextRun.InlineCode inlineCode:
                    CommitString();
                    var ve = Prefabs.MkLineCode.CloneTreeWithoutContainer();
                    ApplyFontStyleToVE(ve);
                    (ve as Label)!.text = inlineCode.Text;
                    into.Add(ve);
                    break;
                case Markdown.TextRun.Italic i:
                    sb.Append("<i>");
                    italic = true;
                    _Stringify(i.Italicized);
                    italic = false;
                    sb.Append("</i>");
                    break;
                case Markdown.TextRun.Link(var textRun, var url):
                    CommitString();
                    ve = Prefabs.MkLineLink.CloneTreeWithoutContainer();
                    ApplyFontStyleToVE(ve);
                    Stringify(textRun, ve, breakOnSpace);
                    ve.RegisterCallback<PointerUpEvent>(evt => {
                        //Logs.Log($"Click {Description}");
                        //button 0, 1, 2 = left, right, middle click
                        //Right click is handled as UIBack in InputManager
                        if (evt.button == 0)
                            Application.OpenURL(url);
                        evt.StopPropagation();
                    });
                    into.Add(ve);
                    break;
                case Markdown.TextRun.Sequence sequence:
                    foreach (var tr in sequence.Pieces)
                        _Stringify(tr);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(t));
            }
        }
        _Stringify(t);
        CommitString();
    }
    private static VisualElement RenderMarkdown(Markdown.Block b) {
        switch (b) {
            case Markdown.Block.CodeBlock(var language, var contents):
                var ve = Prefabs.MkCodeBlock.CloneTreeWithoutContainer();
                var cont = ve.Q("CodeContainer");
                foreach (var line in contents.Split('\n')) {
                    var lve = Prefabs.MkCodeBlockText.CloneTreeWithoutContainer();
                    (lve as Label)!.text = line;
                    cont.Add(lve);
                }
                return ve;
            case Markdown.Block.Empty empty:
                return Prefabs.MkEmpty.CloneTreeWithoutContainer();
            case Markdown.Block.Header header:
                ve = Prefabs.MkHeader.CloneTreeWithoutContainer();
                ve.AddToClassList($"mkHeader{header.Size}");
                Stringify(header.Text, ve);
                return ve;
            case Markdown.Block.HRule hRule:
                throw new Exception("HRule not handled");
            case Markdown.Block.List(var ordered, var lines):
                ve = Prefabs.MkList.CloneTreeWithoutContainer();
                foreach (var (i, opt) in lines.Enumerate()) {
                    var optHtml = Prefabs.MkListOption.CloneTreeWithoutContainer();
                    optHtml.Q<Label>("Marker").text = ordered ? $"{i + 1}." : "-";
                    foreach (var block in opt)
                        optHtml.Q("Blocks").Add(RenderMarkdown(block));
                    ve.Add(optHtml);
                }
                return ve;
            case Markdown.Block.Paragraph paragraph:
                ve = Prefabs.MkParagraph.CloneTreeWithoutContainer();
                foreach (var line in paragraph.Lines) {
                    var html = Prefabs.MkLine.CloneTreeWithoutContainer();
                    Stringify(line, html);
                    ve.Add(html);
                }
                return ve;
            default:
                throw new ArgumentOutOfRangeException(nameof(b));
        }
    }
}
}