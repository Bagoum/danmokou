using System;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using Danmokou.ADV;
using Danmokou.Core;
using UnityEngine;

namespace MiniProjects.VN {

[Serializable]
public record BlessedRainADVData(Suzunoya.Data.InstanceData VNData) : ADVData(VNData) {
    public int rainCount = 7;
}

[CreateAssetMenu(menuName = "Data/ADV/Blessed Rain Game")]
public class BlessedRainGameDef : ADVGameDef {
    public override Task Run(ADVInstance inst) {
        return inst.Manager.ExecuteVN(vn => {
            if (inst.ADVData.VNData.Location is not null)
                vn.LoadToLocation(inst.ADVData.VNData.Location);
            return _VNBlessedRain.VNScriptBlessedRain(vn);
        });
    }

    public override ADVData NewGameData() => new BlessedRainADVData(new(SaveData.r.GlobalVNData));
}
}