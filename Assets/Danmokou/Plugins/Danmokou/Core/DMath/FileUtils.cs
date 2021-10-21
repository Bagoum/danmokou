
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using BagoumLib;
using Danmokou.Core;
using JetBrains.Annotations;
using Newtonsoft.Json;
using ProtoBuf;
using UnityEngine;
#pragma warning disable 168

public static class FileUtils {
    public const string SAVEDIR = "DMK_Saves/";
    public const string AYADIR = SAVEDIR + "Aya/";

    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings() {
        TypeNameHandling = TypeNameHandling.Auto,
        ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
    };

    public static IEnumerable<string> EnumerateDirectory(string dir) {
        CheckDirectory(dir);
        return Directory.EnumerateFiles(dir);
    }

    public static void CheckDirectory(string final) {
        var dir = Path.GetDirectoryName(final);
        if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
    }

    public static T CopyJson<T>(T obj) =>
        JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(obj, JsonSettings), JsonSettings) ?? 
        throw new Exception($"Failed to JSON-copy object {obj} of type {obj?.GetType()}");
    
    public static void WriteJson(string file, object obj) {
        CheckDirectory(file);
        using (StreamWriter sw = new StreamWriter(file)) {
            sw.WriteLine(JsonConvert.SerializeObject(obj, Formatting.Indented, JsonSettings));
        }
    }

    public static void WriteProto(string file, object obj) {
        CheckDirectory(file);
        using (var fw = File.Create(file)) {
            Serializer.Serialize(fw, obj);
        }
    }
    public static void WriteProtoCompressed(string file, object obj) {
        CheckDirectory(file);
        using (var fw = File.Create(file)) {
            DeflateStream(fw, s => Serializer.Serialize(s, obj));
        }
    }

    private static void DeflateStream(Stream target, Action<Stream> writer) {
        var strm = new MemoryStream();
        writer(strm);
        strm.Position = 0;
        var compressed = Compress(strm);
        using (var w = new BinaryWriter(target)) {
            w.Write(compressed);
        }
    }
    private static Stream InflateStream(Stream source) => Decompress(source);
    private static byte[] Compress(Stream input) {
        using (var compressStream = new MemoryStream()) {
            using (var compressor = new DeflateStream(compressStream, CompressionMode.Compress)) {
                input.CopyTo(compressor);
                compressor.Close();
            }
            return compressStream.ToArray();
        }
    }
    
    private static Stream Decompress(Stream input) {
        var output = new MemoryStream();
        using (var decompressor = new DeflateStream(input, CompressionMode.Decompress)) {
            decompressor.CopyTo(output);
        }
        output.Position = 0;
        return output;
    }
    
    public static T? ReadJson<T>(string file) where T : class {
        try {
            using (StreamReader sr = new StreamReader(file)) {
                return JsonConvert.DeserializeObject<T>(sr.ReadToEnd(), JsonSettings);
            }
        } catch (Exception e) {
            Logs.Log($"Couldn't read {typeof(T)} from file {file}. (JSON)", false, LogLevel.WARNING);
            return null;
        }
    }
    public static T? ReadProto<T>(string file) where T : class {
        try {
            using (var fr = File.OpenRead(file)) {
                return Serializer.Deserialize<T>(fr);
            }
        } catch (Exception e) {
            Logs.Log($"Couldn't read {typeof(T)} from file {file}. (PROTO)", false, LogLevel.WARNING);
            return null;
        }
    }
    public static T? ReadProtoCompressed<T>(string file) where T : class {
        try {
            using (var fr = File.OpenRead(file)) {
                return Serializer.Deserialize<T>(InflateStream(fr));
            }
        } catch (Exception e) {
            Logs.Log($"Couldn't read {typeof(T)} from file {file}. (PROTO-C)", false, LogLevel.WARNING);
            return null;
        }
    }
    public static T? ReadProtoCompressed<T>(TextAsset file) where T : class {
        try {
            using (var fr = new MemoryStream(file.bytes)) {
                return Serializer.Deserialize<T>(InflateStream(fr));
            }
        } catch (Exception e) {
            Logs.Log($"Couldn't read {typeof(T)} from textAsset {file.name}. (PROTO-C)", false, LogLevel.WARNING);
            return null;
        }
    }
    

    public static void WriteTex(string file, Texture2D tex) {
        CheckDirectory(file);
        File.WriteAllBytes(file, tex.EncodeToJPG(95));
    }

    public static void WriteString(string file, string text) {
        CheckDirectory(file);
        File.WriteAllText(file, text);
    }

    public static void Destroy(string file) {
        if (File.Exists(file)) File.Delete(file);
    }


    public class InputConfig {
        public KeyCode FocusHold = KeyCode.LeftShift;
        public KeyCode ShootHold = KeyCode.Z;
        public KeyCode Special = KeyCode.X;
        public KeyCode Swap = KeyCode.Space;
    }
    private const string INPUT = SAVEDIR + "input.txt";
    private const string INPUTREF = SAVEDIR + "input_reference.txt";
    public static InputConfig i { get; }

    static FileUtils() {
        var inp = ReadJson<InputConfig>(INPUT);
        if (inp == null) {
            inp = new InputConfig();
            WriteJson(INPUT, inp);
            var kcs = Enum.GetValues(typeof(KeyCode)).Cast<KeyCode>().Select(kc => $"{kc.ToString()} = {(int) kc}").ToArray();
            WriteJson(INPUTREF, kcs);
        }
        i = inp;
    }
}