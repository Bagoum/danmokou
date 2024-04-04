using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive;
using System.Text;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Culture;
using BagoumLib.Events;
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
public static partial class XMLHelpers {
    public static UXMLReferences Prefabs => GameManagement.UXMLPrefabs;

    public static EventedBinder<T> Binder<T>(this Evented<T> ev) => new(ev, null);
    
    /// <summary>
    /// Configure a DMK prefab-based background for use with a <see cref="UIScreen"/>.
    /// </summary>
    public static UIScreen WithBG(this UIScreen screen,
        (GameObject prefab, BackgroundTransition transition)? background) {
        if (background.Try(out var bg)) {
            screen.WithOnEnterStart(fromNull => {
                var bgo = ServiceLocator.FindOrNull<IBackgroundOrchestrator>();
                bgo?.QueueTransition(bg.transition);
                bgo?.ConstructTarget(bg.prefab, !fromNull);
            });
        }
        return screen;
    }
    
    public static UIScreen PlaymodeScreen(this UIController m, ICampaignDanmakuGameDef game, UIScreen bossPractice, UIScreen stagePractice, Dictionary<Mode, Sprite> sprites, PlayModeCommentator? commentator, Func<CampaignConfig, Func<SharedInstanceMetadata, bool>, Func<UINode, ICursorState, UIResult>> getMetadata, out bool onlyOneMode) {
        var s = new UIScreen(m, null, UIScreen.Display.Unlined) {
            Builder = (s, ve) => ve.CenterElements()
        }.WithOnEnterStart(_ => { if (commentator != null) commentator.Appear(); })
            .WithOnExitStart(_ => { if (commentator != null) commentator.Disappear(); });
        bool tutorialIncomplete = !SaveData.r.TutorialDone && game.Tutorial != null;
        PlayModeStatus Wrap(Mode m, bool locked) =>
            new(m, locked) { TutorialIncomplete = tutorialIncomplete };
        var campaignComplete = SaveData.r.CampaignCompleted(game.Campaign.Key);
        var unlockedPracticeBosses = PBosses.Length > 0;
        var unlockedPracticeStages = PStages.Length > 0;
        var vm = new AxisViewModel();
        IUIView[] View(PlayModeStatus pm) => new IUIView[] { new AxisView(vm), 
            new PlaymodeView(new(pm), commentator, sprites) };
        //the AddVE is an anchor located in the center of the screen
        var axisGroup = new UIColumn(new UIRenderConstructed(s, new(x => x.AddVE(null))), 
            new UINode {
                OnConfirm = getMetadata(game.Campaign, meta => 
                    InstanceRequest.RunCampaign(MainCampaign, null, meta)),
            }.Bind(View(Wrap(Mode.MAIN, false))), 
        (game.ExCampaign != null ? 
            new UINode {
                EnabledIf = () => campaignComplete,
                OnConfirm = getMetadata(game.ExCampaign, meta => 
                    InstanceRequest.RunCampaign(ExtraCampaign, null, meta)),
            } : null)?.Bind(View(Wrap(Mode.EX, !campaignComplete))), 
        (PracticeBossesExist ?
            new UINode {
                EnabledIf = () => unlockedPracticeBosses,
                OnConfirm = (_, _) => new UIResult.GoToNode(bossPractice),
            } : null)?.Bind(View(Wrap(Mode.BOSSPRAC, !unlockedPracticeBosses))), 
        (PracticeStagesExist ?
            new UINode {
                EnabledIf = () => unlockedPracticeStages,
                OnConfirm = (_, _) => new UIResult.GoToNode(stagePractice),
            } : null)?.Bind(View(Wrap(Mode.STAGEPRAC, !unlockedPracticeStages))), 
        (game.Tutorial != null ? new UINode {
            OnConfirm = (_, _) => new UIResult.StayOnNode(!InstanceRequest.RunTutorial(game)),
        } : null)?.Bind(View(Wrap(Mode.TUTORIAL, false)))) {
            EntryIndexOverride = () => tutorialIncomplete ? -1 : 0
        };
        s.SetFirst(axisGroup);
        onlyOneMode = axisGroup.Nodes.Count == 1;
        return s;
    }

    private class PlaymodeViewModel : IConstUIViewModel {
        public PlayModeStatus mode { get; }
        public PlaymodeViewModel(PlayModeStatus mode) {
            this.mode = mode;
        }
    }
    private class PlaymodeView : UIView<PlaymodeViewModel>, IUIView {
        public override VisualTreeAsset? Prefab => References.uxmlDefaults.FloatingNode;
        private readonly PlayModeCommentator? commentator;
        private readonly Dictionary<Mode, Sprite> sprites;

        public PlaymodeView(PlaymodeViewModel viewModel, PlayModeCommentator? commentator, Dictionary<Mode, Sprite> sprites) : base(viewModel) {
            this.commentator = commentator;
            this.sprites = sprites;
        }

        public override void OnBuilt(UINode node) {
            base.OnBuilt(node);
            Node.HTML.ConfigureFloatingImage(sprites[VM.mode.Mode]);
        }

        void IUIView.OnEnter(UINode node, ICursorState cs, bool animate) {
            if (commentator != null)
                commentator.SetCommentFromValue(VM.mode);
        }
    }
    
    public static UIScreen StagePracticeScreen(this UIController m,
        Func<CampaignConfig, Func<SharedInstanceMetadata, bool>, Func<UINode, ICursorState, UIResult>> getMetadata) {
        var s = new UIScreen(m, "STAGE PRACTICE") {Builder = (s, ve) => {
            s.Margin.SetLRMargin(720, 720);
            ve.AddColumn().style.width = 30f.Percent();
            ve.AddColumn().style.width = 70f.Percent();
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
        Func<CampaignConfig, Func<SharedInstanceMetadata, bool>, Func<UINode, ICursorState, UIResult>> getMetadata) {
        var cmpSpellHist = SaveData.r.GetCampaignSpellHistory();
        var prcSpellHist = SaveData.r.GetPracticeSpellHistory();

        var s = new UIScreen(m, "BOSS PRACTICE") {Builder = (_, ve) => {
            ve.AddScrollColumn().style.width = 30f.Percent();
            ve.AddScrollColumn().style.width = 70f.Percent();
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
                                n.HTML.Q<Label>("CampaignHistory").text = $"{cs}/{ct}";
                                n.HTML.Q<Label>("PracticeHistory").text = $"{ps}/{pt}";
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
            Builder = (s, _) => {
                s.HTML.Q("HeaderRow").SetLRMargin(-80, -80);
            }
        }.WithOnExitEnd(_ => SaveData.AssignSettingsChanges());
        //To support a setup where the top row does not scroll, we do as follows:
        //Controls container (column)
        // - Top row (row)
        // - Controls space (scroll column)
        //   - Binding rows ([row])
        var controlsContainer =
            new UIRenderConstructed(s, Prefabs.UIScreenColumn, (_, ve) => ve.style.width = 100f.Percent());
        var controlsHeader = new UIRenderConstructed(controlsContainer, Prefabs.UIScreenRow);
        var controlsSpace = new UIRenderConstructed(controlsContainer, 
            new(parent => parent.AddZeroPaddingScrollColumn())).ColumnRender(0);
        UINode NodeForBinding(RebindableInputBinding b, int index, KeyRebindInputNode.Mode mode) {
            return new FuncNode(null, n => {
                if (b.ProtectedIndices.Contains(index))
                    return PopupUIGroup.CreatePopup(n, $"Keybinding for \"{b.Purpose}\"",
                        r => new UIColumn(r,
                                new UINode("You cannot rebind this key.") { Prefab = Prefabs.PureTextNode })
                            { Interactable = false }, new PopupButtonOpts.Centered(null));
                
                Maybe<IInspectableInputBinding>? newTempBinding = null;
                return PopupUIGroup.CreatePopup(n, $"Keybinding for \"{b.Purpose}\"",
                    r => new UIColumn(r, new UINode {
                                Prefab = GameManagement.References.uxmlDefaults.PureTextNode, Passthrough = true
                            }.WithCSS(fontControlsClass)
                            .Bind(new LabelView<(string? curr, bool hasNext, string? next)>(new(
                                () => (b.Sources[index]?.Description, newTempBinding != null, 
                                    newTempBinding?.ValueOrNull()?.Description), cn => {
                                    var show = $"Current binding: {cn.curr ?? "(No binding)"}";
                                    if (cn.hasNext)
                                        show += $"\nNew binding: {cn.next ?? "(No binding)"}";
                                    return show;
                                })
                            )),
                        new KeyRebindInputNode(LString.Empty, keys => 
                                newTempBinding = keys == null ? 
                                    Maybe<IInspectableInputBinding>.None :
                                    new(SimultaneousInputBinding.FromMany(keys)), mode)
                            .WithCSS(noSpacePrefixClass, centerTextClass)),
                    new PopupButtonOpts.LeftRightFlush(null, new UINode[] {
                        new UIButton("Unassign", UIButton.ButtonType.Confirm, _ => {
                            newTempBinding = Maybe<IInspectableInputBinding>.None;
                            return new UIResult.StayOnNode();
                        }),
                        new UIButton(LocalizedStrings.Controls.confirm, UIButton.ButtonType.Confirm, _ => {
                            if (newTempBinding is { } nb) {
                                b.ChangeBindingAt(index, nb.ValueOrNull());
                                InputSettings.SaveInputConfig();
                            }
                            return new UIResult.ReturnToTargetGroupCaller(n.Group);
                        })
                    })
                );
            }) { OnBuilt = n => n.HTML.style.width = 30f.Percent() }
                .WithCSS(small1Class, fontControlsClass)
                .Bind(new SimpleLabelView(() => b.Sources[index]?.Description ?? "(No binding)"));
        }
        UIGroup[] MakeBindings(IEnumerable<RebindableInputBinding> src, KeyRebindInputNode.Mode mode) => 
            src.Select(b => (UIGroup)new UIRow(
                controlsSpace.Construct(Prefabs.UIScreenRow),
                new PassthroughNode(b.Purpose) {
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
            new PassthroughNode("Keyboard Bindings").WithCSS(large1Class));
        var (kbm, ctrlr) = References.gameDefinition.GetRebindableControls();
        var kbBindings = MakeBindings(kbm, KeyRebindInputNode.Mode.KBM);
        var cBindingsLead = new UIRow(controlsSpace.Construct(Prefabs.UIScreenRow), 
            new PassthroughNode("Controller Bindings").WithCSS(large1Class));
        var cBindings = MakeBindings(ctrlr, KeyRebindInputNode.Mode.Controller);
        
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
        PropTwoWayBinder<T> SB<T>(string prop) => new(SaveData.s, prop, null);
        s.SetFirst(new UIRow(new UIRenderExplicit(s.ScreenRender, ve => ve.Q("HeaderRow")), new[] {
            new UINode("<cspace=16>GAME</cspace>") {
                Prefab = GameManagement.UXMLPrefabs.HeaderNode,
                //Using UIRenderConstructed allows making different "screens" for each options page
                ShowHideGroup = new UIColumn(new UIRenderConstructed(s, Prefabs.UIScreenColumn, 
                        (_, ve) => ve.style.maxWidth = new Length(60, LengthUnit.Percent)), 
                    allowStaticOptions ?
                            new LROptionNode<string?>(main_lang, SaveData.s.TextLocale, new[] {
                            (LText.Make("English"), Locales.EN),
                            (LText.Make("日本語"), Locales.JP)
                        }) :
                        null,
                    allowStaticOptions ?
                        new LROptionNode<bool>(smoothing, SB<bool>(nameof(SaveData.s.AllowInputLinearization)), OnOffOption) :
                        null,
                    new LROptionNode<float>(screenshake, SB<float>(nameof(SaveData.s.Screenshake)), new(LString, float)[] {
                            ("Off", 0),
                            ("x0.5", 0.5f),
                            ("x1", 1f),
                            ("x1.5", 1.5f),
                            ("x2", 2f)
                        }),
                    new LROptionNode<bool>(hitbox, SB<bool>(nameof(SaveData.s.UnfocusedHitbox)), new[] {
                        (hitbox_always, true),
                        (hitbox_focus, false)
                    }),
                    new LROptionNode<bool>(backgrounds, SaveData.s.Backgrounds, OnOffOption),
                    allowStaticOptions ?
                        new LROptionNode<float>(dialogue_speed, 
                            new PropTwoWayBinder<float>(SaveData.VNSettings, nameof(SaveData.VNSettings.TextSpeed), null)
                            , new(LString, float)[] {
                            ("x2", 2f),
                            ("x1.5", 1.5f),
                            ("x1", 1f),
                            ("x0.75", 0.75f),
                            ("x0.5", 0.5f),
                        }) :
                        null,
                    new LROptionNode<float>(dialogue_opacity, SB<float>(nameof(SaveData.s.VNDialogueOpacity)), 11.Range().Select(x =>
                        (LText.Make($"{x * 10}"), x / 10f)).ToArray()),
                    new LROptionNode<bool>(dialogue_skip, SB<bool>(nameof(SaveData.s.VNOnlyFastforwardReadText)), new[] {
                        (dialogue_skip_read, true),
                        (dialogue_skip_all, false)
                    })
                )
            },
            new UINode("<cspace=16>GRAPHICS</cspace>") {
                Prefab = GameManagement.UXMLPrefabs.HeaderNode,
                ShowHideGroup = new UIColumn(new UIRenderConstructed(s, Prefabs.UIScreenColumn, 
                    (_, ve) => ve.style.maxWidth = new Length(60, LengthUnit.Percent)), 
                    new LROptionNode<bool>(shaders, SB<bool>(nameof(SaveData.s.Shaders)), new[] {
                        (shaders_low, false),
                        (shaders_high, true)
                    }),
                    new LROptionNode<(int, int)>(resolution, SaveData.s.Resolution, new (LString, (int, int))[] {
                        ("3840x2160", (3840, 2160)),
                        ("2560x1440", (2560, 1440)),
                        ("1920x1080", (1920, 1080)),
                        ("1600x900", (1600, 900)),
                        ("1280x720", (1280, 720)),
                        ("848x477", (848, 477)),
                        ("640x360", (640, 360))
                    }),
                    new LROptionNode<FullScreenMode>(fullscreen, SaveData.s.Fullscreen, new[] {
                        (fullscreen_exclusive, FullScreenMode.ExclusiveFullScreen),
                        (fullscreen_borderless, FullScreenMode.FullScreenWindow),
                        (fullscreen_window, FullScreenMode.Windowed),
                    }),
                    new LROptionNode<int>(vsync, SB<int>(nameof(SaveData.s.Vsync)), new[] {
                        (generic_off, 0),
                        (generic_on, 1),
                        //(vsync_double, 2)
                    })
#if !WEBGL
                    , new LROptionNode<bool>(LocalizedStrings.UI.renderer, SB<bool>(nameof(SaveData.s.LegacyRenderer)), new[] {
                        (renderer_legacy, true),
                        (renderer_normal, false)
                    })
#endif
                )
            },
            new UINode("<cspace=16>SOUND</cspace>") {
                Prefab = GameManagement.UXMLPrefabs.HeaderNode,
                ShowHideGroup = new UIColumn(new UIRenderConstructed(s, Prefabs.UIScreenColumn, 
                    (_, ve) => ve.style.maxWidth = new Length(60, LengthUnit.Percent)),
                    new LROptionNode<float>(master_volume, SaveData.s.MasterVolume, 21.Range().Select(x =>
                        ((LString)$"{x * 10}", x / 10f)).ToArray()),
                    new LROptionNode<float>(bgm_volume, SaveData.s._BGMVolume, 21.Range().Select(x =>
                        ((LString)$"{x * 10}", x / 10f)).ToArray()),
                    new LROptionNode<float>(sfx_volume, SaveData.s._SEVolume, 21.Range().Select(x =>
                        ((LString)$"{x * 10}", x / 10f)).ToArray()),
                    new LROptionNode<float>("Dialogue Typing Volume", SaveData.s._VNTypingSoundVolume, 21.Range().Select(x =>
                        ((LString)$"{x * 10}", x / 10f)).ToArray())
                )
            }, controlsGroup
        }));
        return s;
    }

    public static UIScreen SaveLoadVNScreen(this UIController m, Func<SerializedSave, bool>? loader, Func<int, SerializedSave>? saver, bool loadIsDangerous=true) {
        int perPage = 8;
        UINode CreateSaveLoadEntry(int i) {
            return new FuncNode(null, n => {
                var ind = n.IndexInGroup;
                var save = SaveData.v.Saves.TryGetValue(i, out var _s) ? _s : null;
                if (saver == null && (loader == null || save == null))
                    return new UIResult.StayOnNode(true);
                return PopupUIGroup.CreatePopup(n, saveload_header,
                    r => new UIColumn(r,
                            new UINode(saveload_what_do_ls(i + 1))
                                { Prefab = Prefabs.PureTextNode })
                        { Interactable = false },
                    new PopupButtonOpts.LeftRightFlush(null, new UINode?[] {
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
                                    return n.ReturnToGroup;
                                })
                    }));
            }) {
                Prefab = Prefabs.SaveLoadNode
            }.Bind(new SaveLoadDataView(new(i)));
        }

        var s = new UIScreen(m, "SAVE/LOAD") {
            Builder = (s, ve) => {
                s.Header.style.marginRight = 100;
                s.Margin.SetLRMargin(360, 360);
                var c1 = ve.AddColumn();
                var c2 = ve.AddColumn();
                c1.style.justifyContent = c2.style.justifyContent = Justify.SpaceBetween;
                c1.style.width = c2.style.width = 50f.Percent();
                c1.style.paddingRight = c2.style.paddingLeft = 10;
            }
        };
        
        UINode CreatePage(int p) {
            var c1 = new UIColumn(s, new UIRenderColumn(s, 0)) {
                LazyNodes = () => (p * perPage, p * perPage + perPage / 2)
                    .Range().Select(CreateSaveLoadEntry)
            };
            var c2 = new UIColumn(s, new UIRenderColumn(s, 1)) {
                LazyNodes = () => (p * perPage + perPage / 2, (p + 1) * perPage)
                    .Range().Select(CreateSaveLoadEntry)
            };
            return new UINode($"{p + 1}") {
                Prefab = Prefabs.HeaderNode,
                OnBuilt = n => {
                    var l = n.HTML.Q<Label>();
                    l.style.unityTextAlign = TextAnchor.MiddleCenter;
                    l.style.fontSize = 100;
                    n.BodyHTML!.SetPadding(0, 25, 0, 25);
                },
                ShowHideGroup = new HGroup(c1, c2) { EntryNodeOverride = new(() => c1.EntryNode) }
            };
        }

        var pages = 9.Range().Select(CreatePage).ToArray();
        s.SetFirst(new UIRow(new UIRenderExplicit(s.ScreenRender, ve => ve.Q("HeaderRow")), pages));
        return s;
    }
    public static UIScreen ReplayScreen(this UIController m, UIScreen gameDetails) {
        var s = new UIScreen(m, "REPLAYS") {
            Builder = (_, ve) => ve.AddScrollColumn()
        };
        s.SetFirst(new UIColumn(s, null) {
            LazyNodes = () => SaveData.p.ReplayData.Select(rep => {
                return new FuncNode(rep.Metadata.Record.AsDisplay(true, true), n => {
                    var ind = n.Group.Nodes.IndexOf(n);
                    return PopupUIGroup.CreatePopup(n, replay_window,
                        r => new UIColumn(r,
                                new UINode(replay_what_do_ls(rep.Metadata.Record.CustomName))
                                    { Prefab = Prefabs.PureTextNode })
                            { Interactable = false },
                        new PopupButtonOpts.LeftRightFlush(null, new UINode[] {
                            UIButton.Delete(() => {
                                if (SaveData.p.TryDeleteReplay(rep)) {
                                    n.Remove();
                                    return true;
                                } else return false;
                            }, () => new UIResult.GoToNode(n.Group, ind)),
                            new UIButton(view_details, UIButton.ButtonType.Confirm, _ => 
                                n.ReturnToGroup.Then(CreateGameResultsView(rep.Metadata.Record, gameDetails))),
                            new UIButton(replay_view, UIButton.ButtonType.Confirm, _ => {
                                s.Controller.ConfirmCache();
                                return new UIResult.StayOnNode(!InstanceRequest.ViewReplay(rep));
                            })
                        }));
                }) {
                    CacheOnEnter = true
                }.WithCSS(monospaceClass, small2Class, centerTextClass);
            })
        });
        return s;
    }

    private class RecordsScreenFilter : VersionedUIViewModel {
        private InstanceMode _mode = InstanceMode.CAMPAIGN;
        public InstanceMode Mode {
            get => _mode;
            set {
                _mode = value;
                ModelChanged();
            } 
        }
        private int _cmpIndex;
        public int CmpIndex {
            get => _cmpIndex;
            set {
                _cmpIndex = value;
                campaign = campaigns[_cmpIndex].Key;
                Stage = 0;
                if (campaigns[_cmpIndex].bosses.Length > 0)
                    Boss = campaigns[_cmpIndex].bosses[0].boss.key;
                else
                    throw new Exception("No high score handling for days menu implemented yet"); //AssignBoss(days!.bosses[]);
                ModelChanged();
            }
        }
        public string campaign = null!;
        private string _boss = null!;
        public string Boss {
            get => _boss;
            set {
                _boss = value;
                bphase = 0;
                ModelChanged();
            }
        }
        public int bphase;
        private int _stage;
        public int Stage {
            get => _stage;
            set {
                _stage = value;
                sphase = 0;
                ModelChanged();
            }
        }
        public int sphase;

        private readonly SMAnalysis.AnalyzedCampaign[] campaigns;
        public RecordsScreenFilter(SMAnalysis.AnalyzedCampaign[] campaigns) {
            this.campaigns = campaigns;
            CmpIndex = 0;
        }
    }
    public static UIScreen RecordsScreen(this UIController menu, UIScreen replayScreen, UIScreen detailsScreen,
        SMAnalysis.AnalyzedCampaign[] campaigns, SMAnalysis.AnalyzedDayCampaign? days = null) {
        var screen = new UIScreen(menu, "RECORDS") {Builder = (s, ve) => {
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
        var m = new RecordsScreenFilter(campaigns);
        bool Matches(LowInstanceRequestKey key) => m.Mode switch {
            InstanceMode.CAMPAIGN => key is CampaignRequestKey cr && cr.Campaign == m.campaign,
            InstanceMode.BOSS_PRACTICE => key is BossPracticeRequestKey br && 
                                          br.Campaign == m.campaign && br.Boss == m.Boss && br.PhaseIndex == m.bphase,
            InstanceMode.STAGE_PRACTICE => key is StagePracticeRequestKey sr && 
                                           sr.Campaign == m.campaign && sr.StageIndex == m.Stage && sr.PhaseIndex == m.sphase,
            InstanceMode.SCENE_CHALLENGE => key is PhaseChallengeRequestKey sc &&
                                            sc.Campaign == m.campaign && sc.Boss == m.Boss && sc.PhaseIndex == m.bphase,
            _ => throw new Exception($"No high score screen handling for key of type {key.GetType()}")
        };
        
        PropTwoWayBinder<T> VM<T>(string prop) => new(m, prop);
        
        var scoreNodes = SaveData.r.FinishedGames
            //If the user doesn't enter a name on the replay screen, the score won't show up, but it will still be recorded internally
            .Where(g => !string.IsNullOrWhiteSpace(g.CustomNameOrPartial) && g.Score > 0)
            .OrderByDescending(g => g.Score).Select(g =>
                //Don't need to show the request (eg. Yukari (Ex) p3) because it's shown by the option nodes above this
                new FuncNode(g.AsDisplay(true, false), n => PopupUIGroup.CreatePopup(
                        n, record_header, 
                        r => new UIColumn(r,new UINode(record_what_do(g.CustomNameOrPartial)) 
                            { Prefab = Prefabs.PureTextNode} ) { Interactable = false },
                        new PopupButtonOpts.LeftRightFlush(null, new UINode[] {
                            new UIButton(view_details, UIButton.ButtonType.Confirm, _ =>
                                n.ReturnToGroup.Then(CreateGameResultsView(g, detailsScreen))),
                            new UIButton(record_view_replay, UIButton.ButtonType.Confirm, _ => {
                                foreach (var (ir, replay) in SaveData.p.ReplayData.Enumerate())
                                    if (replay.Metadata.Record.Uuid == g.Uuid)
                                        return n.ReturnToGroup.Then(new UIResult.GoToNode(replayScreen.Groups[0], ir));
                                return new UIResult.StayOnNode(true);
                            }) { EnabledIf = 
                                SaveData.p.ReplayData.Any(rep => rep.Metadata.Record.Uuid == g.Uuid).Freeze() }
                        })
                    )) {
                    VisibleIf = () => Matches(g.RequestKey)
                }.WithCSS(monospaceClass, small2Class, centerTextClass)
                 .WithRootView(v => v.ViewModel.NodeIsVisibleHash = () => m.ViewVersion));
        bool IsBossOrChallenge() => m.Mode is InstanceMode.BOSS_PRACTICE or InstanceMode.SCENE_CHALLENGE;
        bool IsStage() => m.Mode == InstanceMode.STAGE_PRACTICE;
        var optnodes = new UINode[] {
            new LROptionNode<InstanceMode>(practice_type, VM<InstanceMode>(nameof(m.Mode)), new[] {
                (practice_m_campaign, InstanceMode.CAMPAIGN),
                (practice_m_boss, InstanceMode.BOSS_PRACTICE),
                days == null ? ((LString, InstanceMode)?) null : (practice_m_scene, InstanceMode.SCENE_CHALLENGE),
                (practice_m_stage, InstanceMode.STAGE_PRACTICE)
            }.FilterNone().ToArray()),
            new LROptionNode<int>(practice_campaign, VM<int>(nameof(m.CmpIndex)),
                campaigns.Select((c, i) => ((LString)c.campaign.shortTitle, i)).ToArray()),
            new LROptionNode<string>(practice_m_whichboss, VM<string>(nameof(m.Boss)), () =>
                    IsBossOrChallenge() ?
                        campaigns[m.CmpIndex].bosses.Select(b => (b.boss.BossPracticeName, b.boss.key)).ToArray() :
                        new (LString, string)[] {("", "")} //required to avoid errors with the option node
                ){ VisibleIf = IsBossOrChallenge },
            new LROptionNode<int>(practice_m_whichstage, VM<int>(nameof(m.Stage)), () =>
                    IsStage() ?
                        campaigns[m.CmpIndex].stages.Select((s, i) => ((LString)s.stage.stageNumber, i)).ToArray() :
                        new (LString, int)[] {("", 0)} //required to avoid errors with the option node
                ){ VisibleIf = IsStage },
            new LROptionNode<int>(practice_m_whichphase, VM<int>(nameof(m.bphase)), () =>
                    IsBossOrChallenge() ?
                        campaigns[m.CmpIndex].bossKeyMap[m.Boss].Phases.Select(
                            //p.index is used as request key
                            (p, i) => ((LString)$"{i + 1}. {p.Title}", p.index)).ToArray() :
                        new (LString, int)[] {("", 0)}) {
                    OnBuilt = n => n.HTML.Q("ValueContainer").style.width = new StyleLength(new Length(80, LengthUnit.Percent)),
                    VisibleIf = IsBossOrChallenge
                },
            new LROptionNode<int>(practice_m_whichphase, VM<int>(nameof(m.sphase)), () =>
                    IsStage() ?
                        campaigns[m.CmpIndex].stages[m.Stage].Phases.Select(
                            p => (p.Title, p.index)).Prepend((practice_fullstage, 1)).ToArray() :
                        new (LString, int)[] {("", 0)}) {
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

    private class PlayerSelect : VersionedUIViewModel {
        private ShipConfig _player = null!;
        public ShipConfig Player {
            get => _player;
            set {
                _player = value; 
                ModelChanged(); //changes the available supports/shots
            }
        }
        public (LString, IAbilityCfg)[] PlayerSupports =>
            Player.supports.Select(s =>
                ((LString)s.ordinal, (IAbilityCfg)s.ability)).ToArray();
        public IAbilityCfg support = null!;
        
        public (LString, ShotConfig)[] PlayerShots =>
            Player.shots2.Select(s => ((LString)(s.shot.isMultiShot ?
                shotsel_multi(s.ordinal) :
                shotsel_type(s.ordinal)), s.shot)).ToArray();
        public ShotConfig shot = null!;
        
        public Subshot subshot = Subshot.TYPE_D;
    }
    [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
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
        var initPlayer = c.campaign.players[0];
        var p = new PlayerSelect() {
            Player = initPlayer,
            support = initPlayer.supports[0].ability,
            shot = initPlayer.shots2[0].shot
        };

        var team = new TeamConfig(0, Subshot.TYPE_D,
            (from pl in c.campaign.players
            from s in pl.shots2
            from a in pl.supports
            select (pl, s.shot, a.ability as IAbilityCfg)).ToArray());
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
            var effShot = p.shot.GetSubshot(p.subshot);
            ReplayActor r;
            if (effShot.demoReplay != null) {
                r = Replayer.BeginReplaying(new Replayer.ReplayerConfig(
                    Replayer.ReplayFinishMethod.REPEAT, 
                    effShot.demoReplay.Frames
                ) { OnFinish = () => demoPlayer!.transform.position = new Vector2(0, -3) });
                demoCT?.Cancel();
                demoCT = new Cancellable();
                if (effShot.demoSetupSM != null) {
                    var esm = StateMachineManager.FFromText(effShot.demoSetupSM);
                    esm.Start(new SMHandoff(demoSetup, demoCT));
                }
            } else {
                r = Replayer.BeginReplaying(new Replayer.ReplayerConfig(
                    Replayer.ReplayFinishMethod.REPEAT,
                    () => new []{new FrameInput(0, 0, 0)}
                ));
            }
            GameManagement.NewInstance(InstanceMode.NULL, InstanceFeatures.ShotDemoFeatures, 
                new InstanceRequest((_, __) => { }, smeta, new CampaignRequest(c)), r);
            if (demoPlayer == null) {
                demoPlayer = UnityEngine.Object.Instantiate(demoPlayerPrefab).GetComponent<PlayerController>();
            }
            demoPlayer.UpdateTeam((p.Player, p.shot, p.support), p.subshot, true);
            demoPlayer.transform.position = new Vector2(0, -3);
        }
        
        (ShipConfig player, FancyShotDisplay display)[] displays = c.campaign.players.Select(p => {
            var display = UnityEngine.Object.Instantiate(p.shotDisplay, shotDisplayContainer).GetComponent<FancyShotDisplay>();
            display.Show(false);
            return (p, display);
        }).ToArray();
        
        void ShowShot(bool first) {
            if (!first) UpdateDemo();
            var index = displays.IndexOf(sd => sd.player == p.Player);
            displays[index].display.SetShot(p.Player, p.shot, p.subshot, p.support);
            foreach (var (i, (_, display)) in displays.Enumerate()) {
                //Only show the selected player on entry so the others don't randomly appear on screen during swipe
                display.Show(!first || i == index);
                display.SetRelative(i, index, first);
            }
        }

        PropTwoWayBinder<T> VM<T>(string prop) => new(p, prop);
        
        var playerSelect = new LROptionNode<ShipConfig>(LString.Empty, VM<ShipConfig>(nameof(p.Player)),
            c.campaign.players.Select(p => (p.ShortTitle, p)).ToArray());

        var supportSelect = new LROptionNode<IAbilityCfg>(LString.Empty, VM<IAbilityCfg>(nameof(p.support)),
            () => playerSelect.Value.supports.Select(s => 
                ((LString)s.ordinal, (IAbilityCfg)s.ability)).ToArray());
        var shotSelect = new LROptionNode<ShotConfig>(LString.Empty, VM<ShotConfig>(nameof(p.shot)), () =>
                playerSelect.Value.shots2.Select(s => ((LString)(s.shot.isMultiShot ? 
                        shotsel_multi(s.ordinal) : 
                        shotsel_type(s.ordinal)), s.shot)).ToArray());
        var subshotSelect = new LROptionNode<Subshot>(LString.Empty, VM<Subshot>(nameof(p.subshot)),
            EnumHelpers2.Subshots.Select(x => (shotsel_variant_ls(x.Describe()), x)).ToArray()) 
            {VisibleIf = () => p.shot.isMultiShot };

        var screen = new UIScreen(m, null, UIScreen.Display.Unlined) {
                Builder = (s, ve) => {
                    s.Margin.style.marginLeft = 160;
                    var g = ve.AddColumn();
                    g.style.maxWidth = new Length(25, LengthUnit.Percent);
                    g.style.paddingTop = 720;
                },
            }.WithOnEnterStart(_ => ShowShot(true))
            .WithOnEnterEnd(_ => UpdateDemo())
            .WithOnExitStart(_ => {
                CleanupDemo();
                foreach (var (player, display) in displays) {
                    if (player != playerSelect.Value) display.Show(false);
                }
            });
        screen.Tokens.Add(p.ViewVersion.OnChange.Subscribe(_ => ShowShot(false)));
        _ = new UIColumn(screen, null,
            new PassthroughNode(shotsel_player).WithCSS(centerTextClass),
            playerSelect.WithCSS(optionNoKeyClass),
            new PassthroughNode(LString.Empty),
            new PassthroughNode(shotsel_shot).WithCSS(centerTextClass),
            shotSelect.WithCSS(optionNoKeyClass),
            subshotSelect.WithCSS(optionNoKeyClass),
            new PassthroughNode(LString.Empty),
            new PassthroughNode(shotsel_support).WithCSS(centerTextClass),
            supportSelect.WithCSS(optionNoKeyClass),
            new PassthroughNode(LString.Empty),
            new FuncNode(play_game, () => continuation(new TeamConfig(0, 
                p.subshot, (p.Player, p.shot, p.support)))).WithCSS(centerTextClass)
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
        var dfc = new DifficultySettings(null);
        var viewModel = new VersionedUIViewModel();
        //dfc may be reloaded, use () => to avoid keeping reference to a single one
        //normally we wouldn't need to share the view model, but we need to keep track of ModelChanged on dfc load
        PropTwoWayBinder<T> VM<T>(string prop) => new(() => dfc, prop, viewModel);
        void SetNewDFC(DifficultySettings? newDfc) {
            if (newDfc == null) return;
            dfc = FileUtils.CopyJson(newDfc);
            viewModel.ModelChanged();
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
        UINode MakeOption<T>(LString title, IEnumerable<(LString, T)> options, string dfcProp, LString description) {
            return new LROptionNode<T>(title, VM<T>(dfcProp), options.ToArray()) {
                ShowHideGroup = new UIColumn(descCol, new UINode(LString.Format("\n\n{0}", description))) {
                    Interactable = false
                },
                OnBuilt = n => {
                    n.HTML.style.paddingLeft = 20;
                    n.HTML.style.paddingRight = 20;
                },
            }.WithCSS(small1Class);
        }
        UINode MakePctOption(LString title, string dfcProp, LString description)
            => MakeOption(title, pctMods, dfcProp, description);
        UINode MakeOnOffOption(LString title, string dfcProp, LString description)
            => MakeOption(title, yesNo, dfcProp, description);
        UINode MakeOptionAuto<T>(LString title, IEnumerable<T> options, string dfcProp, LString description)
            => MakeOption(title, options.Select(x => ((LString)(x!.ToString()), x)), dfcProp, description);

        var saved = SaveData.s.DifficultySettings;
        
        UINode MakeSaveLoadDFCNode((string name, DifficultySettings settings) s) =>
            new FuncNode(s.name,
                n => {
                    var ind = n.Group.Nodes.IndexOf(n);
                    return PopupUIGroup.CreatePopup(n, setting,
                        r => new UIColumn(r, new UINode(setting_what_do_ls(s.name)) {Prefab = Prefabs.PureTextNode}) 
                            { Interactable = false },
                        new PopupButtonOpts.LeftRightFlush(null, new UINode[] {
                            UIButton.Delete(() => {
                                if (SaveData.s.RemoveDifficultySettings(s)) {
                                    n.Remove();
                                    return true;
                                } else return false;
                            }, () => new UIResult.GoToNode(n.Group, ind)),
                            UIButton.Load(() => {
                                SetNewDFC(s.settings);
                                return true;
                            }, n.ReturnToGroup),
                        })
                    );
                });
        
        var optSliderHelper = new PassthroughNode()
            .Bind(new LabelView<int>(new(() => dfc.customValueSlider, 
                x => desc_effective_ls(effective, DifficultySettings.FancifySlider(x)))));
        dfc.respawnOnDeath = false;
        screen.SetFirst(new UIColumn(screen, null,
            MakeOption(scaling, (DifficultySettings.MIN_SLIDER, DifficultySettings.MAX_SLIDER + 1).Range()
                .Select(x => ((LString)($"{x}"), x)), nameof(dfc.customValueSlider), desc_scaling),
            optSliderHelper.WithCSS(small2Class),
            MakeOptionAuto(suicide, new[] {0, 1, 3, 5, 7}, nameof(dfc.numSuicideBullets), desc_suicide),
            MakePctOption(p_dmg, nameof(dfc.playerDamageMod), desc_p_dmg),
            MakePctOption(boss_hp, nameof(dfc.bossHPMod), desc_boss_hp),
            MakeOnOffOption(respawn, nameof(dfc.respawnOnDeath), desc_respawn),
            MakePctOption(faith_decay, nameof(dfc.faithDecayMultiplier), desc_faith_decay),
            MakePctOption(faith_acquire, nameof(dfc.faithAcquireMultiplier), desc_faith_acquire),
            MakePctOption(meter_usage, nameof(dfc.meterUsageMultiplier), desc_meter_usage),
            MakePctOption(meter_acquire, nameof(dfc.meterAcquireMultiplier), desc_meter_acquire),
            MakeOnOffOption(bombs_enabled, nameof(dfc.bombsEnabled), desc_bombs_enabled),
            MakeOnOffOption(meter_enabled, nameof(dfc.meterEnabled), desc_meter_enabled),
            MakePctOption(player_speed, nameof(dfc.playerSpeedMultiplier), desc_player_speed),
            MakePctOption(player_hitbox, nameof(dfc.playerHitboxMultiplier), desc_player_hitbox),
            MakePctOption(player_grazebox, nameof(dfc.playerGrazeboxMultiplier), desc_player_grazebox),
            MakeOption(lives, (1, 14).Range().Select(x => ((LString)($"{x}"), (int?) x)).Prepend((generic_default, null)),
                nameof(dfc.startingLives), desc_lives),
            MakeOption(poc, AddPlus(new[] {
                    //can't use addition to generate these because -6 + 0.4 =/= -5.6...
                    -6, -5.6, -5.2, -4.8, -4.4, -4, -3.6, -3.2, -2.8, -2.4, -2, -1.6, -1.2, -0.8, -0.4,
                    0, 0.4, 0.8, 1.2, 1.6, 2
                }), nameof(dfc.pocOffset), desc_poc),
            //new PassthroughNode(""),
            new UINode(to_select) { OnConfirm = (_, _) => dfcCont(dfc) } ,
            new UINode(manage_setting) {
                ShowHideGroup = new UIColumn(descCol, 
                    saved.Select(MakeSaveLoadDFCNode)
                        .Prepend(new FuncNode(create_setting, 
                            n => {
                                var settingNameEntry = new TextInputNode(LString.Empty);
                                return PopupUIGroup.CreatePopup(n, create_setting,
                                    r => new UIColumn(r, new UINode(new_setting_name) {
                                        Prefab = Prefabs.PureTextNode, Passthrough = true
                                    }, settingNameEntry),
                                    new PopupButtonOpts.LeftRightFlush(null, new UINode[] {
                                        UIButton.Save(() => {
                                            SaveData.s.AddDifficultySettings(settingNameEntry.DataWIP, dfc);
                                            n.Group.AddNodeDynamic(MakeSaveLoadDFCNode(saved.Last()));
                                            return true;
                                        }, n.ReturnToGroup), 
                                    }));
                            }) {
                            OnBuilt = n => n.HTML.style.marginBottom = 120
                        }))
            }
        ));
        return screen;
    }

    private class StatsScreenFilter : VersionedUIViewModel {
        private int? _cmpIndex;
        public int? campaignIndex {
            get => _cmpIndex;
            set {
                _cmpIndex = value;
                boss = null;
                ModelChanged();
            }
        }
        public string? boss;
        public Maybe<FixedDifficulty?> difficultySwitch = Maybe<FixedDifficulty?>.None;
        public ShipConfig? playerSwitch = null;
        public (ShipConfig, ShotConfig)? shotSwitch = null;
    }
    
    public static UIScreen StatisticsScreen(this UIController menu, IDanmakuGameDef game, IEnumerable<InstanceRecord> allGames, 
        SMAnalysis.AnalyzedCampaign[] campaigns) {
        InstanceRecord[] games = allGames.ToArray();
        var f = new StatsScreenFilter();
        PropTwoWayBinder<T> VM<T>(string prop) => new(f, prop);
        bool Filter(InstanceRecord ir) =>
            (f.campaignIndex == null ||
             campaigns[f.campaignIndex.Value].Key == ir.RequestKey.Campaign) &&
            (!f.difficultySwitch.Valid || f.difficultySwitch.Value == ir.SharedInstanceMetadata.difficulty.standard) &&
            (f.playerSwitch == null || f.playerSwitch == ir.SharedInstanceMetadata.team.ships[0].ship) &&
            //don't include check for ability for now
            (f.shotSwitch is not { } ss ||
             (ss.Item1 == ir.SharedInstanceMetadata.team.ships[0].ship &&
              ss.Item2 == ir.SharedInstanceMetadata.team.ships[0].shot))
            ;

        Statistics.StatsGenerator stats = default!;
        void UpdateStats() {
            stats = new Statistics.StatsGenerator(games.Where(Filter), campaigns, cbp =>
                (f.campaignIndex == null || (campaigns[f.campaignIndex.Value].Key == cbp.Campaign)) &&
                (f.boss == null || (f.boss == cbp.Boss)));
        }
        ILabelViewModel Show(Func<string> stat) =>
            new LabelViewModel<string>(stat, x=>x) { OverrideHashHandler = () => f.ViewVersion };

        string AsPct(float f01) => $"{(int) (f01 * 100)}%";
        LString ShowCard((BossPracticeRequest card, float ratio) bpr) {
            return LString.Format(
                "{0}: {1}", 
                (LString)(AsPct(bpr.ratio)),
                bpr.card.phase.Title
            );
        }

        var optNodes = new UINode[] {
            new LROptionNode<int?>(practice_campaign, VM<int?>(nameof(f.campaignIndex)),
                campaigns
                    .Select((c, i) => ((LString)(c.campaign.shortTitle), (int?)i))
                    .Prepend((stats_allcampaigns, null))
                    .ToArray()),
            new LROptionNode<Maybe<FixedDifficulty?>>(stats_seldifficulty, VM<Maybe<FixedDifficulty?>>(nameof(f.difficultySwitch)),
                GameManagement.CustomAndVisibleDifficulties
                    .Select(x => (x?.Describe() ?? difficulty_custom, Maybe<FixedDifficulty?>.Of(x)))
                    .Prepend((stats_alldifficulty, Maybe<FixedDifficulty?>.None)).ToArray()),
            new LROptionNode<ShipConfig?>(stats_selplayer, VM<ShipConfig?>(nameof(f.playerSwitch)),
                game.AllShips
                    .Select(x => (x.ShortTitle, (ShipConfig?)x))
                    .Prepend((stats_allplayers, null)).ToArray()),
            new LROptionNode<(ShipConfig, ShotConfig)?>(stats_selshot, 
                VM<(ShipConfig, ShotConfig)?>(nameof(f.shotSwitch)),
                game.AllShips
                    .SelectMany(p => p.shots2
                        .Select(os => (ShotConfig.PlayerShotDescription(p, os.shot),
                            ((ShipConfig, ShotConfig)?)(p, os.shot))))
                    .Prepend((stats_allshots, null)).ToArray()),
        };
        var statsNodes = new UINode[] {
            new TwoLabelUINode(stats_allruns, Show(() => $"{stats.TotalRuns}")),
            new TwoLabelUINode(stats_complete, Show(() => $"{stats.CompletedRuns}")),
            new TwoLabelUINode(stats_1cc, Show(() => $"{stats.OneCCRuns}")),
            new TwoLabelUINode(stats_deaths, Show(() => $"{stats.TotalDeaths}")),
            new TwoLabelUINode(stats_totaltime, Show(() => stats.TotalFrames.FramesToTime())),
            new TwoLabelUINode(stats_avgtime, Show(() => stats.AvgFrames.FramesToTime())),
            new TwoLabelUINode(stats_favday, Show(() => 
                stats.TotalRuns == 0 ? generic_na :
                (LString)($"{stats.FavoriteDay.Item1} ({stats.FavoriteDay.Item2.Length})"))),
            new TwoLabelUINode(stats_favplayer, Show(() => 
                stats.TotalRuns == 0 ? generic_na :
                    LString.Format(
                "{0} ({1})", stats.FavoriteShip.Item1.ShortTitle, (LString)($"{stats.FavoriteShip.Item2.Length}")
            ))),
            new TwoLabelUINode(stats_favshot, Show(() => {
                if (stats.TotalRuns == 0) return generic_na;
                var ((pc, sc, ab), recs) = stats.FavoriteShot;
                return LString.Format("{0} {1} ({1})", 
                    ShotConfig.PlayerShotDescription(pc, sc),
                    ab?.Value.ShortTitle ?? new(null, "No ability"),
                    $"{recs.Length}"
                );
            })),
            new TwoLabelUINode(stats_highestscore, Show(() => 
                stats.TotalRuns == 0 ? generic_na.Value : $"{stats.MaxScore}")),
            new TwoLabelUINode(stats_capturerate, Show(() => AsPct(stats.CaptureRate))),
            new TwoLabelUINode(stats_bestcard, Show(() => 
                !stats.HasSpellHist ? generic_na : ShowCard(stats.BestCapture))),
            new TwoLabelUINode(stats_worstcard, Show(() => 
                !stats.HasSpellHist ? generic_na : ShowCard(stats.WorstCapture)))
        }.Select(x => x.WithCSS(small1Class));
        
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
        screen.Tokens.Add(f.ViewVersion.Subscribe(_ => UpdateStats()));
        screen.SetFirst(new VGroup(
                new UIColumn(screen.ColumnRender(1), optNodes),
                new UIColumn(screen.ColumnRender(2), statsNodes)
            ) {
            EntryNodeOverride = optNodes[0]
        });
        return screen;
    }

    private class AchievementViewModel : CSSClassViewModel {
        public Achievement Achv { get; }

        public AchievementViewModel(Achievement a) : base(() => a.Completed, CheckmarkClass(true), CheckmarkClass(false)) {
            this.Achv = a;
        }
        public override long GetViewHash() => (Achv.VisibleDescription, Achv.Completed).GetHashCode();
    }

    private class AchievementView : CssClassView {
        private readonly AchievementViewModel viewModel;
        public AchievementView(AchievementViewModel viewModel) : base(viewModel) {
            this.viewModel = viewModel;
        }

        public override void OnBuilt(UINode node) {
            base.OnBuilt(node);
            node.HTML.style.paddingLeft = 20;
            node.HTML.style.paddingRight = 20;
        }

        protected override BindingResult Update(in BindingContext context) {
            Node.HTML.Q<Label>("Description").text = viewModel.Achv.VisibleDescription;
            return base.Update(in context);
        }
    }
    public static UIScreen AchievementsScreen(this UIController menu, VisualTreeAsset node, 
        AchievementManager acvs) {
        var screen = new UIScreen(menu, "ACHIEVEMENTS") {Builder = (s, ve) => {
            s.Margin.SetLRMargin(600, 720);
            ve.AddScrollColumn();
        }};
        _ = new UIColumn(screen, null, acvs.SortedAchievements.Select(a =>
                new UINode(a.Title) {
                        Prefab = node
                }.Bind(new AchievementView(new(a)))
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
        AudioTrackSet? ts = null;
        screen.SetFirst(new UIColumn(screen, null, musics.SelectNotNull(m => (m.DisplayInMusicRoom switch {
            true => new FuncNode(string.Format("({0}) {1}", m.TrackPlayLocation, m.Title), 
                () => {
                    ts?.FadeOut(1, AudioTrackState.DestroyReady);
                    ts = ServiceLocator.Find<IAudioTrackService>().AddTrackset();
                    return new UIResult.StayOnNode(ts.AddTrack(m) is null);
                }) {
                ShowHideGroup = new UIColumn(descCol, 
                    new UINode(m.MusicRoomDescription).WithCSS(small2Class, fontUbuntuClass)) 
                {Interactable = false}
            },
            false => new UINode("????????????????") {
                ShowHideGroup = new UIColumn(descCol, 
                    new UINode("This track is not yet unlocked.").WithCSS(small2Class, fontUbuntuClass))
                {Interactable = false}
            },
            _ => null
        })?.WithCSS(small1Class))));
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
            .Prepend(new TwoLabelUINode("Name", () => rec.CustomNameOrPartial, null))
            .Append(new UINode("") {Passthrough = true}).Concat(new TwoLabelUINode?[] {
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
        ) { ExitNodeOverride = options[0], EntryNodeOverride = options[0] };
        resultsScreen.Tokens.Add(resultsScreen.OnExitEnd.SubscribeOnce(_ => details.Destroy()));
        resultsScreen.SetFirst(details);
        return new UIResult.GoToNode(details);
    }

    public static readonly Action<UIScreen, VisualElement> GameResultsScreenBuilder = (s, ve) => {
        s.Margin.SetLRMargin(600, 600);
        var inner = ve.AddColumn();
        var lr = inner.AddRow();
        lr.style.height = 90f.Percent();
        var left = lr.AddColumn().SetPadding(20, 50, 50, 50);
        left.style.width = 50f.Percent();
        left.name = "Left";
        var right = lr.AddColumn().SetPadding(20, 50, 50, 50);
        right.style.width = 50f.Percent();
        right.name = "Right";
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
        var finishedAnyCampaign = FinishedCampaigns.Any();
        s.SetFirst(new UIColumn(s, null,
                new TransferNode(main_scores, records) {
                        EnabledIf = () => finishedAnyCampaign,
                        OnBuilt = setMargin
                    }.WithCSS(large2Class),
                new TransferNode(main_stats, stats) {
                            EnabledIf = () => finishedAnyCampaign,
                            OnBuilt = setMargin
                }.WithCSS(large2Class),
                achievements == null ? null :
                    new TransferNode(main_achievements, achievements) {
                            OnBuilt = setMargin
                        }.WithCSS(large2Class),
                new TransferNode(main_replays, replays) {
                        EnabledIf = () => SaveData.p.ReplayData.Count > 0,
                        OnBuilt = setMargin
                    }.WithCSS(large2Class)
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
        screen.SetFirst(new UIRow(new UIRenderExplicit(screen, ve => ve.Q("Options"))) {
            LazyNodes = () => licenses
                .Where(l => l.file != null)
                .Select(l => {
                    var render = new UIRenderConstructed(contRender, Prefabs.UIScreenScrollColumn, 
                        (_, ve) => {
                            ve.style.width = 100f.Percent();
                            ve.SetPadding(0, 180, 0, 210);
                        });
                    return new UINode(l.name) {
                        ShowHideGroup = new UIColumn(render),
                        OnBuilt = _ => {
                            foreach (var b in MarkdownParser.Parse(l.file.text)) {
                                render.HTML.Q<ScrollView>().Add(RenderMarkdown(b));
                            }
                        }
                    };
                })
        });
        return screen;
    }

    private static void Stringify(Markdown.TextRun t, VisualElement into, bool breakOnSpace=false) {
        var sb = new StringBuilder();
        var pxPerSpace = 11;
        bool bold = false;
        bool italic = false;
        void CommitString() {
            if (sb.Length == 0) return;
            var ve = Prefabs.MkLineText.CloneTreeNoContainer();
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
                    var ve = Prefabs.MkLineCode.CloneTreeNoContainer();
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
                    ve = Prefabs.MkLineLink.CloneTreeNoContainer();
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
                var ve = Prefabs.MkCodeBlock.CloneTreeNoContainer();
                var cont = ve.Q("CodeContainer");
                var step = 1;
                var clines = contents.Split('\n');
                //we cannot make one text block for the entire contents since we will get the error
                // 'A VisualElement must not allocate more than 65535 vertices.'
                //Instead we split them into smaller groups.
                //Experimentally, it seems like giving each line its own prefab is fastest.
                for (int ii = 0; ii < clines.Length; ii += step) {
                    var lve = Prefabs.MkCodeBlockText.CloneTreeNoContainer();
                    (lve as Label)!.text = string.Join("\n", clines[ii..Math.Min(ii + step, clines.Length)]);
                    cont.Add(lve);
                }
                return ve;
            case Markdown.Block.Empty empty:
                return Prefabs.MkEmpty.CloneTreeNoContainer();
            case Markdown.Block.Header header:
                ve = Prefabs.MkHeader.CloneTreeNoContainer();
                ve.AddToClassList($"mkHeader{header.Size}");
                Stringify(header.Text, ve);
                return ve;
            case Markdown.Block.HRule hRule:
                throw new Exception("HRule not handled");
            case Markdown.Block.List(var ordered, var lines):
                ve = Prefabs.MkList.CloneTreeNoContainer();
                foreach (var (i, opt) in lines.Enumerate()) {
                    var optHtml = Prefabs.MkListOption.CloneTreeNoContainer();
                    optHtml.Q<Label>("Marker").text = ordered ? $"{i + 1}." : "-";
                    foreach (var block in opt)
                        optHtml.Q("Blocks").Add(RenderMarkdown(block));
                    ve.Add(optHtml);
                }
                return ve;
            case Markdown.Block.Paragraph paragraph:
                ve = Prefabs.MkParagraph.CloneTreeNoContainer();
                foreach (var line in paragraph.Lines) {
                    var html = Prefabs.MkLine.CloneTreeNoContainer();
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