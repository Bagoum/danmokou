using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BagoumLib;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Scenes;
using Danmokou.VN;
using JetBrains.Annotations;
using Newtonsoft.Json;
using ProtoBuf;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;

namespace Danmokou.Player {

[Serializable]
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class AyaPhoto {
    public SerializedImage Image { get; set; } = null!;
    public float Angle { get; set; }
    public float ScreenWidth { get; set; }
    public float ScreenHeight { get; set; }
    
    /// <summary>
    /// Filename of the photo, relative to the photo directory, with no suffix.
    /// </summary>
    public string Filename { get; set; }
    
    [JsonIgnore] [ProtoIgnore]
    public float PPU => Image.PixelWidth / ScreenWidth;

    /// <summary>
    /// JSON constructor, do not use
    /// </summary>
    [Obsolete]
#pragma warning disable 8618
    private AyaPhoto() { }
#pragma warning restore 8618

    /// <summary>
    /// Create a photo.
    /// </summary>
    /// <param name="photo">Texture information. Ownership of this texture is passed to this object.</param>
    /// <param name="rect">Rect describing the capture location on the screen (16x9 resolution).</param>
    /// <param name="saveToDisk">True if the image file should be saved to disk. Note that if false, this photo
    ///  cannot be reloaded from serialization once destroyed.</param>
    public AyaPhoto(Texture2D photo, CRect rect, bool saveToDisk) {
        Filename = $"{DateTime.Now.FileableTime()}-{RNG.RandStringOffFrame()}";
        Image = new($"{FileUtils.AYADIR}{Filename}.jpg", 
            photo, saveToDisk);
        ScreenWidth = rect.halfW * 2;
        ScreenHeight = rect.halfH * 2;
        Angle = rect.angle;
        SceneIntermediary.SceneUnloaded.SubscribeOnce(_ => Image.UnloadTex());
    }


    public bool TryLoad(out Sprite sprite) {
        sprite = null!;
        if (Image.TryLoad(out Texture2D? t)) {
            sprite = Sprite.Create(t, new Rect(0, 0, Image.PixelWidth, Image.PixelHeight), 
                new Vector2(0.5f, 0.5f), PPU, 0, SpriteMeshType.FullRect);
            return true;
        } else return false;
    }
}
}