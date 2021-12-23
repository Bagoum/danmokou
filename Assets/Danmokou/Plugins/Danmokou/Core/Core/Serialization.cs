using System;
using System.IO;
using BagoumLib;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Danmokou.Core {

/// <summary>
/// A representation of a user-generated image that is saved to disk.
/// <br/>This representation itself can be serialized.
/// </summary>
[Serializable]
public class SerializedImage {
    /// <summary>
    /// Full filename (relative to project root) of the save location.
    /// </summary>
    public string FullFilename { get; set; }
    public GraphicsFormat Format { get; set; }
    
    public int PixelWidth { get; set; }
    public int PixelHeight { get; set; }
    
    private Texture2D? tex = null;

    [JsonIgnore]
    public Texture2D Texture => TryLoad(out var t) ? t : throw new Exception($"Couldn't load image {FullFilename}");
    
    /// <summary>
    /// JSON constructor, do not use
    /// </summary>
    [Obsolete]
#pragma warning disable 8618
    private SerializedImage() { }
#pragma warning restore 8618
    

    /// <summary>
    /// </summary>
    /// <param name="fullFilename">Full filename (relative to project root) of the save location.</param>
    /// <param name="tex">Texture data.</param>
    /// <param name="saveToDisk">True if the image file should be saved to disk. Note that if false, this photo
    ///  cannot be reloaded from serialization once destroyed.</param>
    public SerializedImage(string fullFilename, Texture2D tex, bool saveToDisk) {
        FullFilename = fullFilename;
        this.tex = tex;
        Format = tex.graphicsFormat;
        PixelWidth = tex.width;
        PixelHeight = tex.height;
        
        //For debugging purposes, always write the texture if in editor
#if !UNITY_EDITOR
        if (saveToDisk)
#endif
            FileUtils.WriteTex(FullFilename, tex);
    }


    public bool TryLoad(out Texture2D texture) {
        if (tex != null) {
            texture = tex;
            return true;
        }
        byte[] data;
        try {
            data = FileUtils.ReadAllBytes(FullFilename);
        } catch (Exception e) {
            Logs.Log($"Couldn't load Aya photo {FullFilename}: {e.Message}", level: LogLevel.WARNING);
            texture = null!;
            return false;
        }
        try {
            tex = texture = new Texture2D(PixelWidth, PixelHeight, Format, TextureCreationFlags.None);
            texture.LoadImage(data);
        } catch (Exception e) {
            Logs.Log($"Couldn't read data from Aya photo {FullFilename}: {e.Message}", 
                level: LogLevel.WARNING);
            texture = null!;
            return false;
        }
        return true;
    }
    
    
    public void UnloadTex() {
        if (tex != null)
            UnityEngine.Object.Destroy(tex);
        tex = null;
    }

    public void RemoveFromDisk() {
        FileUtils.Delete(FullFilename);
    }
}
}