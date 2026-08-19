namespace Trading.Core.Tests;

[TestFixture]
public sealed class ControlledCiFailureTests
{
    [Test]
    public void ControlledFailureProvesCiRejectsAFailingTest()
    {
        Assert.That(
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS"),
            Is.Null,
            "S1-014 controlled CI validation failure.");
    }
}
