namespace Trading.Engine.Tests;

public sealed class TestInfrastructureTests
{
    [Test]
    public void NUnitDiscoversEngineTests() =>
        Assert.That(TestContext.CurrentContext.Test.Name, Is.EqualTo(nameof(NUnitDiscoversEngineTests)));
}
