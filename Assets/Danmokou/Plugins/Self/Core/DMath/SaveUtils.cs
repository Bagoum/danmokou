
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using DMK.Core;
using JetBrains.Annotations;
using Newtonsoft.Json;
using ProtoBuf;
using UnityEngine;
#pragma warning disable 168

public static class SaveUtils {
    public const string DIR = "DMK_Saves/";
    public const string AYADIR = "DMK_Saves/Aya/";


    public static IEnumerable<string> EnumerateDirectory(string dir) {
        dir = DIR + dir;
        CheckDirectory(dir);
        return Directory.EnumerateFiles(dir).Select(f => f.Substring(DIR.Length));
    }

    public static void CheckDirectory(string final) {
        var dir = Path.GetDirectoryName(final);
        if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
    }
    public static void WriteJson(string file, object obj) {
        file = DIR + file;
        CheckDirectory(file);
        using (StreamWriter sw = new StreamWriter(file)) {
            sw.WriteLine(JsonConvert.SerializeObject(obj, Formatting.Indented));
        }
    }

    public static void WriteProto(string file, object obj) {
        file = DIR + file;
        CheckDirectory(file);
        using (var fw = File.Create(file)) {
            Serializer.Serialize(fw, obj);
        }
    }
    public static void WriteProtoCompressed(string file, object obj) {
        file = DIR + file;
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
    
    [CanBeNull]
    public static T ReadJson<T>(string file) where T : class {
        try {
            using (StreamReader sr = new StreamReader($"{DIR}{file}")) {
                return JsonConvert.DeserializeObject<T>(sr.ReadToEnd());
            }
        } catch (Exception e) {
            Log.Unity($"Couldn't read {typeof(T)} from file {DIR}{file}. (JSON)", false, Log.Level.WARNING);
            return null;
        }
    }
    [CanBeNull]
    public static T ReadProto<T>(string file) where T : class {
        try {
            using (var fr = File.OpenRead($"{DIR}{file}")) {
                return Serializer.Deserialize<T>(fr);
            }
        } catch (Exception e) {
            Log.Unity($"Couldn't read {typeof(T)} from file {DIR}{file}. (PROTO)", false, Log.Level.WARNING);
            return null;
        }
    }
    [CanBeNull]
    public static T ReadProtoCompressed<T>(string file) where T : class {
        try {
            using (var fr = File.OpenRead($"{DIR}{file}")) {
                return Serializer.Deserialize<T>(InflateStream(fr));
            }
        } catch (Exception e) {
            Log.Unity($"Couldn't read {typeof(T)} from file {DIR}{file}. (PROTO-C)", false, Log.Level.WARNING);
            return null;
        }
    }

    public static void WriteTex(string file, Texture2D tex) {
        CheckDirectory(file);
        File.WriteAllBytes(file, tex.EncodeToJPG(95));
    }

    public static void Destroy(string file) {
        if (File.Exists(file)) File.Delete(file);
    }


    public class InputConfig {
        public KeyCode FocusHold = KeyCode.LeftShift;
        public KeyCode ShootHold = KeyCode.Z;
        public KeyCode Bomb = KeyCode.X;
    }
    private const string INPUT = "input.txt";
    private const string INPUTREF = "input_reference.txt";
    public static InputConfig i { get; }

    static SaveUtils() {
        i = ReadJson<InputConfig>(INPUT);
        if (i == null) {
            i = new InputConfig();
            WriteJson(INPUT, i);
            var kcs = Enum.GetValues(typeof(KeyCode)).Cast<KeyCode>().Select(kc => $"{kc.ToString()} = {(int) kc}").ToArray();
            WriteJson(INPUTREF, kcs);
        }
    }
}