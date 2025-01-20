using BagoumLib;
using BagoumLib.Events;
using Danmokou.Core;
using Danmokou.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Danmokou.Testing {
[SetUpFixture]
public class TestSetup {
    [OneTimeSetUp]
    public void RunBeforeAnyTests() {
        RHelper.REFLECT_IN_EDITOR = true;
        _ = new DMKLanguageServiceProvider();
        Logs.SetupTestMode();
        var startup = Reflector.STARTUP_PHASE;
        var loc = SaveData.s.TextLocale;
        Logs.Log($"Test reflector startup (should be 3): {startup}; locale: {loc.Value}");
    }

    [OneTimeTearDown]
    public void RunAfterAnyTests() {
        RHelper.REFLECT_IN_EDITOR = false;
        Logs.CloseLog();
    }
}    
}