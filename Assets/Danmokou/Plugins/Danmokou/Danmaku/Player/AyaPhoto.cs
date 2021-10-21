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
    public string Filename { get; set; } = null!;
    public float Angle { get; set; }
    public int PixelWidth { get; set; }
    public int PixelHeight { get; set; }
    public float ScreenWidth { get; set; }
    public float ScreenHeight { get; set; }
    public GraphicsFormat Format { get; set; }
    public bool KeepAlive { get; set; }
    private Texture2D? tex = null;
    
    [JsonIgnore] [ProtoIgnore]
    private string FullFilename => $"{FileUtils.AYADIR}{Filename}.jpg";
    [JsonIgnore] [ProtoIgnore]
    public float PPU => PixelWidth / ScreenWidth;
    
    private static List<AyaPhoto> allPhotos = new List<AyaPhoto>();

    public static void ClearTextures() {
        allPhotos = allPhotos.Where(p => {
            //Remove texture from memory
            if (p.tex != null) Object.Destroy(p.tex);
            //Destroy unsuccessful screenshots
            if (!p.KeepAlive) FileUtils.Destroy(p.FullFilename);
            return p.KeepAlive;
        }).ToList();
    }
    /// <summary>
    /// JSON constructor, do not use
    /// </summary>
    [Obsolete]
    private AyaPhoto() { }
    public AyaPhoto(Texture2D photo, CRect rect) {
        Filename = $"{DateTime.Now.FileableTime()}-{RNG.RandStringOffFrame()}";
        PixelWidth = photo.width;
        PixelHeight = photo.height;
        ScreenWidth = rect.halfW * 2;
        ScreenHeight = rect.halfH * 2;
        Angle = rect.angle;
        Format = photo.graphicsFormat;
        KeepAlive = false;
        FileUtils.WriteTex(FullFilename, photo);
        tex = photo;
        allPhotos.Add(this);
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