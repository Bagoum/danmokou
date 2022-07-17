using Danmokou.Reflection;
using NUnit.Framework;

namespace Danmokou.Testing {
[SetUpFixture]
public class TestSetup {

    [OneTimeSetUp]
    public void RunBeforeAnyTests() => RHelper.REFLECT_IN_EDITOR = true;

    [OneTimeTearDown]
    public void RunAfterAnyTests() => RHelper.REFLECT_IN_EDITOR = false;

}    
}