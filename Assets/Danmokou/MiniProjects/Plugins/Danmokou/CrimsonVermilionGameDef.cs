using System;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using Danmokou.ADV;
using Danmokou.Core;
using UnityEngine;

namespace MiniProjects.VN {


[CreateAssetMenu(menuName = "Data/ADV/Crimson Verm. Game")]
public class CrimsonVermilionGameDef : ADVGameDef {
    public override IExecutingADV Setup(ADVInstance inst) {
        if (inst.Request.LoadProxyData?.VNData is { Location: { } l} replayer)
            inst.VN.LoadToLocation(l, replayer, () => {
                inst.Request.FinalizeProxyLoad();
            });
        return new BarebonesExecutingADV<BlessedRainADVData>(inst, () => 
            inst.Manager.ExecuteVN(_VNCrimsonVermilion.VNScriptCrimsonVermilion1(inst.VN)));
    }

    public override ADVData NewGameData() => new(new(SaveData.r.GlobalVNData));
}
}