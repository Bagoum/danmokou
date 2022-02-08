using System;
using System.Collections.Generic;
using BagoumLib.Cancellation;
using BagoumLib.Events;
using BagoumLib.Tasks;
using Danmokou.Core;
using Danmokou.Scenes;
using Danmokou.Services;
using Danmokou.VN;
using SuzunoyaUnity;

namespace Danmokou.ADV {
public record ADVInstance(ADVInstanceRequest Request, DMKVNState VN, ExecutingVN eVN, Cancellable Tracker) { 
    public ADVData ADVData => Request.ADVData;
    public ADVManager Manager => Request.Manager;
    public Event<ADVData> DataUpdated { get; } = new();
    public void Cancel() {
        Tracker.Cancel();
        VN.DeleteAll(); //this cascades into destroying executingVN
        
    }

    public void UpdateData(Action<ADVData> updater) {
        updater(ADVData);
        DataUpdated.OnNext(ADVData);
    }
    public void UpdateData<T>(Action<T> updater) where T : ADVData {
        updater(ADVData as T ?? throw new Exception($"ADVData is not of type {typeof(T)}"));
        DataUpdated.OnNext(ADVData);
    }
    
    public bool Finish() {
        return ServiceLocator.Find<ISceneIntermediary>().LoadScene(
            new SceneRequest(GameManagement.References.mainMenu,
                SceneRequest.Reason.FINISH_RETURN,
                VN.DeleteAll
                ));
    }
}
public record ADVInstanceRequest(ADVManager Manager, ADVGameDef Game, ADVData ADVData) {

    public bool Run() {
        var Tracker = new Cancellable();
        ADVInstance inst = null!;
        return ServiceLocator.Find<ISceneIntermediary>().LoadScene(new SceneRequest(
            Game.sceneConfig,
            SceneRequest.Reason.START_ONE,
            Manager.DestroyCurrentInstance,
            () => {
                var vn = new DMKVNState(Tracker, Game.key, ADVData.VNData);
                var evn = ServiceLocator.Find<IVNWrapper>().TrackVN(vn);
                ServiceLocator.Find<IVNBacklog>().TryRegister(evn);
                if (Game.allowVnBackjump)
                    evn.doBacklog = loc => {
                        vn.UpdateSavedata().Location = loc;
                        Manager.Restart(ADVData);
                    };
                inst = new ADVInstance(this, vn, evn, Tracker);
                Manager.SetupInstance(inst);
            },
            () => Game.Run(inst).ContinueWithSync(Tracker.Guard(() => inst.Finish()))));
    }
}
}