using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BagoumLib;
using Danmokou.Core;
using Danmokou.DMath;
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
    public string? Filename { get; set; } = null;
    public float Angle { get; set; }
    public int PixelWidth { get; set; }
    public int PixelHeight { get; set; }
    public float ScreenWidth { get; set; }
    public float ScreenHeight { get; set; }
    public GraphicsFormat Format { get; set; }
    private Texture2D? tex = null;
    
    [JsonIgnore] [ProtoIgnore]
    private string FullFilename => $"{FileUtils.AYADIR}{Filename}.jpg";
    [JsonIgnore] [ProtoIgnore]
    public float PPU => PixelWidth / ScreenWidth;

    /// <summary>
    /// JSON constructor, do not use
    /// </summary>
    [Obsolete]
    private AyaPhoto() { }

    /// <summary>
    /// Create a photo.
    /// </summary>
    /// <param name="photo">Texture information. Ownership of this texture is passed to this object.</param>
    /// <param name="rect">Rect describing the capture location on the screen (16x9 resolution).</param>
    /// <param name="saveToDisk">True if the image file should be saved to disk. Note that if false, this photo
    ///  cannot be reloaded from serialization once destroyed.</param>
    public AyaPhoto(Texture2D photo, CRect rect, bool saveToDisk) {
        Filename = $"{DateTime.Now.FileableTime()}-{RNG.RandStringOffFrame()}";
        PixelWidth = photo.width;
        PixelHeight = photo.height;
        ScreenWidth = rect.halfW * 2;
        ScreenHeight = rect.halfH * 2;
        Angle = rect.angle;
        Format = photo.graphicsFormat;
        tex = photo;
        //For debugging purposes, always write the texture if in editor
#if !UNITY_EDITOR
        if (saveToDisk)
#endif
            FileUtils.WriteTex(FullFilename, photo);
        Events.SceneCleared.SubscribeOnce(_ => DisposeTex());
    }

    private void DisposeTex() {
        if (tex != null)
            Object.Destroy(tex);
        tex = null;
    }

    public bool TryLoad(out Sprite? sprite) {
        sprite = null;
        if (TryLoad(out Texture2D? t)) {
            sprite = Sprite.Create(t, new Rect(0, 0, PixelWidth, PixelHeight), 
                new Vector2(0.5f, 0.5f), PPU, 0, SpriteMeshType.FullRect);
            return true;
        } else return false;
    }

    private bool TryLoad(out Texture2D? texture) {
        if (tex != null) {
            texture = tex;
            return true;
        }
        byte[] data;
        try {
            data = File.ReadAllBytes(FullFilename);
        } catch (Exception e) {
            Logs.Log($"Couldn't load Aya photo {FullFilename}: {e.Message}", level: LogLevel.WARNING);
            texture = null;
            return false;
        }
        try {
            texture = new Texture2D(PixelWidth, PixelHeight, Format, TextureCreationFlags.None);
            texture.LoadImage(data);
            tex = texture;
        } catch (Exception e) {
            Logs.Log($"Couldn't read data from Aya photo {FullFilename}: {e.Message}", 
                level: LogLevel.WARNING);
            texture = null;
            return false;
        }
        return true;
    }
}
}