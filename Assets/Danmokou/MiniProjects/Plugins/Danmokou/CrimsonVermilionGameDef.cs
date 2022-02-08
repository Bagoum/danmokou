using System;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using Danmokou.ADV;
using Danmokou.Core;
using UnityEngine;

namespace MiniProjects.VN {


[CreateAssetMenu(menuName = "Data/ADV/Crimson Verm. Game")]
public class CrimsonVermilionGameDef : ADVGameDef {
    public override Task Run(ADVInstance inst) {
        return inst.Manager.ExecuteVN(vn => {
            if (inst.ADVData.VNData.Location is not null)
                vn.LoadToLocation(inst.ADVData.VNData.Location);
            return _VNCrimsonVermilion.VNScriptCrimsonVermilion1(vn);
        });
    }

    public override ADVData NewGameData() => new(new(SaveData.r.GlobalVNData));
}
}