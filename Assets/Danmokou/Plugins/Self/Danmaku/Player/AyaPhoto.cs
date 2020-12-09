using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DMK.Core;
using DMK.DMath;
using JetBrains.Annotations;
using Newtonsoft.Json;
using ProtoBuf;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;

namespace DMK.Player {

[Serializable]
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class AyaPhoto {
    public string Filename { get; set; }
    public float Angle { get; set; }
    public int PixelWidth { get; set; }
    public int PixelHeight { get; set; }
    public float ScreenWidth { get; set; }
    public float ScreenHeight { get; set; }
    public GraphicsFormat Format { get; set; }
    public bool KeepAlive { get; set; }
    [CanBeNull] private Texture2D tex = null;
    
    [JsonIgnore] [ProtoIgnore]
    private string FullFilename => $"{SaveUtils.AYADIR}{Filename}.jpg";
    [JsonIgnore] [ProtoIgnore]
    public float PPU => PixelWidth / ScreenWidth;
    
    private static List<AyaPhoto> allPhotos = new List<AyaPhoto>();

    public static void ClearTextures() {
        allPhotos = allPhotos.Where(p => {
            //Remove texture from memory
            if (p.tex != null) Object.Destroy(p.tex);
            //Destroy unsuccessful screenshots
            if (!p.KeepAlive) SaveUtils.Destroy(p.FullFilename);
            return p.KeepAlive;
        }).ToList();
    }
    public AyaPhoto() { } //JSON constructor
    public AyaPhoto(Texture2D photo, CRect rect) {
        Filename = $"{DateTime.Now.FileableTime()}-{RNG.RandStringOffFrame()}";
        PixelWidth = photo.width;
        PixelHeight = photo.height;
        ScreenWidth = rect.halfW * 2;
        ScreenHeight = rect.halfH * 2;
        Angle = rect.angle;
        Format = photo.graphicsFormat;
        KeepAlive = false;
        SaveUtils.WriteTex(FullFilename, photo);
        //Photo data may initially have incorrect alphas.
        //We destroy and reload it to prevent this issue.
        Object.Destroy(photo);
        allPhotos.Add(this);
    }

    public bool TryLoad(out Sprite sprite) {
        sprite = null;
        if (TryLoad(out Texture2D t)) {
            sprite = Sprite.Create(t, new Rect(0, 0, PixelWidth, PixelHeight), 
                new Vector2(0.5f, 0.5f), PPU, 0, SpriteMeshType.FullRect);
            return true;
        } else return false;
    }

    private bool TryLoad(out Texture2D texture) {
        if (tex != null) {
            texture = tex;
            return true;
        }
        byte[] data;
        try {
            data = File.ReadAllBytes(FullFilename);
        } catch (Exception e) {
            Log.Unity($"Couldn't load Aya photo {FullFilename}: {e.Message}", level: Log.Level.WARNING);
            texture = null;
            return false;
        }
        try {
            texture = new Texture2D(PixelWidth, PixelHeight, Format, TextureCreationFlags.None);
            texture.LoadImage(data);
            tex = texture;
        } catch (Exception e) {
            Log.Unity($"Couldn't read data from Aya photo {FullFilename}: {e.Message}", 
                level: Log.Level.WARNING);
            texture = null;
            return false;
        }
        return true;
    }
}
}