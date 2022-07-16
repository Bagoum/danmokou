using System;
using System.Linq;
using BagoumLib.Culture;
using UnityEngine;
using static FileUtils;

namespace Danmokou.Core.DInput {
public static class InputSettings {
    //Input data is stored here because it needs to be accessible by the Core
    // assembly, and there is currently no save-data class for the Core assembly.
    private const string INPUT = SAVEDIR + "input.txt";
    private const string INPUTREF = SAVEDIR + "input_reference.txt";
    public static InputConfig i { get; }

    static InputSettings() {
        var inp = ReadJson<InputConfig>(INPUT);
        if (inp == null) {
            inp = new InputConfig();
            WriteJson(INPUT, inp);
            var kcs = Enum.GetValues(typeof(KeyCode)).Cast<KeyCode>().Select(kc => $"{kc.ToString()} = {(int) kc}").ToArray();
            WriteJson(INPUTREF, kcs);
        }
        i = inp;
    }

    public static void SaveInputConfig() {
        WriteJson(INPUT, i);
    }
}
}