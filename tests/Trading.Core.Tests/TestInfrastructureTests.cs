namespace Trading.Core.Tests;

public sealed class TestInfrastructureTests
{
    [Test]
    [Category("Infrastructure")]
    public void NUnitDiscoversUnitTests()
    {
        Assert.That(TestContext.CurrentContext.Test.Name, Is.EqualTo(nameof(NUnitDiscoversUnitTests)));
    }
}
