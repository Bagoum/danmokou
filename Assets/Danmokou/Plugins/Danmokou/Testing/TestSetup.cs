using Danmokou.Core;
using Danmokou.Reflection;
using NUnit.Framework;

namespace Danmokou.Testing {
[SetUpFixture]
public class TestSetup {

    [OneTimeSetUp]
    public void RunBeforeAnyTests() {
        RHelper.REFLECT_IN_EDITOR = true;
        Logs.SetupTestMode();
    }

    [OneTimeTearDown]
    public void RunAfterAnyTests() {
        RHelper.REFLECT_IN_EDITOR = false;
        Logs.CloseLog();
    }
}    
}