using System;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using Danmokou.ADV;
using Danmokou.Core;
using Danmokou.VN;
using Suzunoya.ADV;
using UnityEngine;

namespace MiniProjects.VN {

[Serializable]
public record BlessedRainADVData(Suzunoya.Data.InstanceData VNData) : ADVData(VNData) {
    public int rainCount = 7;
}

[CreateAssetMenu(menuName = "Data/ADV/Blessed Rain Game")]
public class BlessedRainGameDef : ADVGameDef {
    public override IExecutingADV Setup(ADVInstance inst) =>
        new BarebonesExecutingADV<BlessedRainADVData>(inst, () => 
            inst.Manager.ExecuteVN(_VNBlessedRain.VNScriptBlessedRain(inst.VN as DMKVNState ?? throw new Exception())));

    public override ADVData NewGameData() => new BlessedRainADVData(new(SaveData.r.GlobalVNData));
}
}