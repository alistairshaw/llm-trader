namespace Trading.Core.Tests;

[TestFixture]
public sealed class ControlledCiFailureTests
{
    [Test]
    public void Controlled_failure_proves_CI_rejects_a_failing_test()
    {
        Assert.Fail("S1-014 controlled CI validation failure.");
    }
}
