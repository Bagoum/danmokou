﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Tasks;
using Danmokou.Core;
using Danmokou.Services;
using Danmokou.UI;
using Danmokou.UI.XML;
using Danmokou.VN;
using Suzunoya.ADV;
using Suzunoya.ControlFlow;
using Suzunoya.Entities;
using SuzunoyaUnity.Rendering;
using UnityEngine.UIElements;
// ReSharper disable AccessToModifiedClosure

namespace Danmokou.ADV {

/// <summary>
/// Specialization of <see cref="BaseExecutingADV{I,D}"/> with some Danmokou-specific functionality, such as UI handling.
/// <br/>Almost all game configuration is done in abstract method <see cref="BaseExecutingADV{I,D}.ConfigureMapStates"/>.
/// </summary>
public abstract class DMKExecutingADV<I, D> : BaseExecutingADV<I, D>, IRegularUpdater where I : ADVIdealizedState where D : ADVData {
    public new DMKVNState VN { get; }
    /// <summary>
    /// Same as <see cref="VN"/> but easier to type
    /// </summary>
    public DMKVNState vn => VN;
    private readonly UITKRerenderer rerenderer;
    protected readonly XMLDynamicMenu menu;
    //--- Common entities
    protected readonly ADVDialogueBox md;
    protected readonly Narrator narrator;
    protected readonly UnityRenderGroup rg;
    protected readonly UnityRenderGroup rgb;
    //--- Lerpers
    private readonly PushLerper<Vector3> dialogueShowOffset = new((p, n) => (n.Y > p.Y) ? 0.3f : 0.5f);
    private readonly PushLerper<FColor> dialogueShowAlpha = new((p, n) => (n.a > p.a) ? 0.3f : 0.5f);
    
    public DMKExecutingADV(ADVInstance inst) : base(inst) {
        VN = inst.VN as DMKVNState ?? throw new Exception($"Expected DMKVNState, found {inst.VN.GetType()}");
        //probably don't need to add these to tokens as they'll be destroyed with VN destruction.
        //Note that the ADV interactables layer is rendered to screen *by default*--
        //we use UITKRerenderer here because we want to have the rendering fade out and fade in during VN screen
        // fades, as if the interactables are "part of the world" and not "part of the UI". 
        rerenderer = VN.Add(new UITKRerenderer(UIBuilderRenderer.ADV_INTERACTABLES_GROUP), sortingID: 10000);
        tokens.Add(ETime.RegisterRegularUpdater(this));
        
        //Create common entities
        md = VN.Add(new ADVDialogueBox());
        tokens.Add(md.ComputedLocation.AddDisturbance(dialogueShowOffset));
        tokens.Add(md.ComputedTint.AddDisturbance(dialogueShowAlpha));
        HideMD();
        narrator = VN.Add(new Narrator());
        rg = (UnityRenderGroup)VN.DefaultRenderGroup;
        rgb = vn.Add(new UnityRenderGroup("black", 1, true));
        rg.Visible.Value = false;

        bool showOnNextDialogue = false;
        
        //Listen to common events
        tokens.Add(VN.ContextStarted.Subscribe(c => {
            if (VN.Contexts.Count == 0) {
                md.Clear(SpeakFlags.None);
                showOnNextDialogue = true;
            }
        }));
        tokens.Add(md.DialogueStarted.Subscribe(_ => {
            if (showOnNextDialogue) {
                showOnNextDialogue = false;
                ShowMD();
            }
        }));
        tokens.Add(VN.ContextFinished.Subscribe(c => {
            if (VN.Contexts.Count == 0 && VN.VNStateActive) {
                HideMD();
            }
        }));
        menu = ServiceLocator.Find<XMLDynamicMenu>();
        tokens.Add(DataChanged.Subscribe(_ => {
            menu.Redraw();
        }));
        SetupMapStates();
    }
    
    
    protected void HideMD() {
        dialogueShowOffset.Push(new(0f, -0.5f, 0));
        dialogueShowAlpha.Push(new FColor(1, 1, 1, 0));
        md.Active.Value = false;
    }
    protected void ShowMD() {
        dialogueShowOffset.Push(new(0,0,0));
        dialogueShowAlpha.Push(new FColor(1, 1, 1, 1));
        md.Active.Value = true;
    }

    /// <inheritdoc cref="BaseExecutingADV{I,D}.GoToMap"/>
    protected UIResult GoToMapUI(string map, Action<D>? updater = null) {
        var prev = Data.CurrentMap;
        if (prev != map) {
            GoToMap(map, updater);
            return new UIResult.StayOnNode();
        }
        return new UIResult.StayOnNode(true);
    }

    public virtual void RegularUpdate() {
        dialogueShowOffset.Update(ETime.FRAME_TIME);
        dialogueShowAlpha.Update(ETime.FRAME_TIME);
    }
    
    /// <inheritdoc/>
    public override void ADVDataFinalized() { }

    protected SelectionRequest<string> SetupSelector() => SetupSelector<string>(x => x);
    protected SelectionRequest<C> SetupSelector<C>(Func<C, LString> displayer) {
        var selector = VNUtils.SetupSelector(VN, menu, displayer, out var token);
        tokens.Add(token);
        return selector;
    }

    // --- Helpers

    protected InteractableBCtxAssertion InteractableBCTX(BoundedContext<Unit> bctx, Vector3 location = default) =>
        new InteractableBCtxAssertion(Manager, bctx) { Location = location };

}

}