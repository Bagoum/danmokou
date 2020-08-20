
using System;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;

public static class SaveUtils {
    private const string DIR = "Saves/";
    public static void Write(string file, object obj) {
        if (!Directory.Exists(DIR)) Directory.CreateDirectory(DIR);
        using (StreamWriter sw = new StreamWriter($"{DIR}{file}")) {
            sw.WriteLine(JsonConvert.SerializeObject(obj, Formatting.Indented));
        }
    }
    [CanBeNull]
    public static T Read<T>(string file) where T : class {
        try {
            using (StreamReader sr = new StreamReader($"{DIR}{file}")) {
                return JsonConvert.DeserializeObject<T>(sr.ReadToEnd());
            }
        } catch (Exception e) {
            Log.Unity($"Couldn't read {typeof(T)} from file {DIR}{file}.", false, Log.Level.WARNING);
            return null;
        }
    }
    
    
    public class InputConfig {
        public KeyCode FocusHold = KeyCode.LeftShift;
        public KeyCode AimLeft = KeyCode.A;
        public KeyCode AimRight = KeyCode.D;
        public KeyCode AimUp = KeyCode.W;
        public KeyCode AimDown = KeyCode.S;
        public KeyCode ShootToggle = KeyCode.X;
        public KeyCode ShootHold = KeyCode.Z;
    }
    private const string INPUT = "input.txt";
    private const string INPUTREF = "input_reference.txt";
    public static InputConfig i { get; }

    static SaveUtils() {
        i = Read<InputConfig>(INPUT);
        if (i == null) {
            i = new InputConfig();
            Write(INPUT, i);
            var kcs = Enum.GetValues(typeof(KeyCode)).Cast<KeyCode>().Select(kc => $"{kc.ToString()} = {(int) kc}").ToArray();
            Write(INPUTREF, kcs);
        }
    }
}