namespace Trading.Architecture.Tests;

public sealed class TestInfrastructureTests
{
    [Test]
    [Category("Infrastructure")]
    public void NUnitDiscoversArchitectureTests()
    {
        Assert.That(TestContext.CurrentContext.Test.Name, Is.EqualTo(nameof(NUnitDiscoversArchitectureTests)));
    }
}
