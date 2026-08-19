namespace Trading.Core.Tests;

[TestFixture]
public sealed class ControlledCiFailureTests
{
    [Test]
    public void Controlled_failure_proves_CI_rejects_a_failing_test()
    {
        Assert.That(
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS"),
            Is.Null,
            "S1-014 controlled CI validation failure.");
    }
}
