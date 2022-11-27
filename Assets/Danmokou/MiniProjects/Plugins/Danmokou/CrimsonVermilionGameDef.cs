using System;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using Danmokou.ADV;
using Danmokou.Core;
using Danmokou.VN;
using Suzunoya.ADV;
using UnityEngine;

namespace MiniProjects.VN {


[CreateAssetMenu(menuName = "Data/ADV/Crimson Verm. Game")]
public class CrimsonVermilionGameDef : ADVGameDef {
    public override IExecutingADV Setup(ADVInstance inst) =>
        new BarebonesExecutingADV<BlessedRainADVData>(inst, () => 
            inst.Manager.ExecuteVN(_VNCrimsonVermilion.VNScriptCrimsonVermilion1(inst.VN as DMKVNState ?? throw new Exception())));

    public override ADVData NewGameData() => new(new(SaveData.r.GlobalVNData));
}
}