using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using BagoumLib.Tasks;
using Suzunoya;
using Suzunoya.ADV;
using Suzunoya.Assertions;
using Suzunoya.ControlFlow;
using Suzunoya.Data;
using SuzunoyaUnity;
using SuzunoyaUnity.Derived;
using UnityEditor;
using UnityEngine;
using Vector3 = System.Numerics.Vector3;

namespace SZYU.Examples {

public class ExampleADVExecuting : BaseExecutingADV<ADVIdealizedState, ADVData> {
    private UnityVNState vn;
    protected readonly ADVDialogueBox md;
    private readonly PushLerper<Vector3> dialogueShowOffset = new((p, n) => (n.Y > p.Y) ? 0.3f : 0.5f);
    private readonly PushLerper<FColor> dialogueShowAlpha = new((p, n) => (n.a > p.a) ? 0.3f : 0.5f);
    public ExampleADVExecuting(ADVInstance inst) : base(inst) {
        vn = VN as UnityVNState ?? throw new Exception();
        //Create common entities
        md = VN.Add(new ADVDialogueBox());
        tokens.Add(md.ComputedLocation.AddDisturbance(dialogueShowOffset));
        tokens.Add(md.ComputedTint.AddDisturbance(dialogueShowAlpha));
        HideMD();
        
        //Listen to common events
        VN.ContextStarted.Subscribe(c => {
            //If a dialogue is about to start, then show the main dialogue box
            if (VN.Contexts.Count == 0) {
                md.Clear();
                ShowMD();
            }
        });
        VN.ContextFinished.Subscribe(c => {
            //If a dialogue has just completed, hide the main dialogue box
            if (VN.Contexts.Count == 0 && VN.VNStateActive) {
                HideMD();
            }
        });
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
    
    protected override MapStateManager<ADVIdealizedState, ADVData> ConfigureMapStates() {
        var red_alice = Context("red_alice", async () => {
            var alice = vn.Find<ExampleCharacter>();
            await alice.SayC("hello world");
        });
        var ms = new MapStateManager<ADVIdealizedState, ADVData>(this, () => new(this));
        ms.ConfigureMap("Red", (i, d) => {
            i.Assert(new EntityAssertion<ExampleCharacter>(vn) { Location = new(2, 0, 0) });
        });
        return ms;
    }
}

public class ExampleADVSetup : MonoBehaviour, IGlobalVNDataProvider {
    private ADVManager manager = null!;
    private readonly List<IDisposable> tokens = new();
    public TextAsset? loadFrom;
    public TextAsset saveTo = null!;

    //This contains settings and the like, you should save it somewhere persistent based on your game setup
    public GlobalData GlobalVNData => new();

    private void Awake() {
        tokens.Add(ServiceLocator.Register(manager = new ADVManager()));
        tokens.Add(manager.ADVState.Subscribe(s => Debug.Log($"The running ADV state is now {s}.")));
        tokens.Add(ServiceLocator.Register<IGlobalVNDataProvider>(this));
    }

    void Start() {
        //Redirect logs from the libraries to Debug.Log
        tokens.Add(Logging.Logs.Subscribe(lm => {
            if (lm.Exception != null)
                Debug.LogException(lm.Exception);
            else
                Debug.Log(lm.Message);
        }));
        
        var advData = loadFrom == null ?
            new ADVData(new InstanceData(GlobalVNData)) { CurrentMap = "Red" } :
            Serialization.DeserializeJson<ADVData>(loadFrom.text) ?? throw new Exception();
        advData.VNData._SetGlobalData_OnlyUseForInitialization(GlobalVNData);
        
        new ExampleADVInstanceRequest(manager, advData, inst => new ExampleADVExecuting(inst)).Run();
    }
    private void OnDisable() {
        tokens.DisposeAll();
    }

    [ContextMenu("Save")]
    public void Save() {
        File.WriteAllText(AssetDatabase.GetAssetPath(saveTo), Serialization.SerializeJson(manager.GetSaveReadyADVData()));
    }
}
}