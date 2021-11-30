using System;
using System.IO;
using BagoumLib;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Services;
using JetBrains.Annotations;
using Microsoft.SqlServer.Server;
using Newtonsoft.Json;
using Suzunoya.Data;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Danmokou.VN {

public class DMKVNData : InstanceData {
    public DMKVNData(GlobalData global) : base(global) { }
}

[Serializable]
public record SavedInstance {
    public SerializedImage Image { get; init; } = null!;
    public string GameIdentifier { get; init; }
    public DateTime SaveTime { get; init; }
    public string Filename { get; init; }
    public int Slot { get; init; }
    public string Description { get; init; }

    //Don't store this. We need a new object every time we query to avoid sharing references
    //private DMKVNData? data;
    [JsonIgnore] public string DesiredSaveLocation => $"{FileUtils.VNDIR}{Filename}.txt";

    /// <summary>
    /// Json constructor, do not use
    /// </summary>
    [Obsolete]
#pragma warning disable 8618
    public SavedInstance() { }
#pragma warning restore 8618

    public SavedInstance(DMKVNData data, DateTime saveTime, Texture2D tex, int slot, string descr) {
        //this.data = data;
        GameIdentifier = GameManagement.References.gameIdentifier;
        SaveTime = saveTime;
        Filename = $"{slot.PadLZero(3)}-{SaveTime.FileableTime()}";
        FileUtils.WriteJson($"{FileUtils.VNDIR}{Filename}.dat", data);
        Image = new($"{FileUtils.VNDIR}{Filename}.jpg", tex, true);
        Slot = slot;
        Description = descr;
    }

    public DMKVNData GetData(GlobalData? global = null) => InstanceData.Deserialize<DMKVNData>(
        FileUtils.Read($"{FileUtils.VNDIR}{Filename}.dat"), global ?? SaveData.r.GlobalVNData);

    public void RemoveFromDisk() {
        File.Delete($"{FileUtils.VNDIR}{Filename}.dat");
        Image.RemoveFromDisk();
    }
}

}