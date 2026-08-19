using System.Reflection;
using Trading.Engine.Runtime;

namespace Trading.Architecture.Tests;

public sealed class RuntimeContractArchitectureTests
{
    private static readonly string[] ExpectedToolNames = ["GetPortfolioSnapshot", "Finish"];
    private static readonly Type[] Contracts =
    [
        typeof(IUtcClock), typeof(IAsyncDelay), typeof(IHostInstanceIdentityProvider),
        typeof(IRuntimeIdentifierGenerator), typeof(IModelSession), typeof(IToolDispatcher),
    ];

    [Test]
    public void RuntimeContractsExposeOnlyApplicationOwnedAndSystemTypes()
    {
        var violations = Contracts.SelectMany(TypeGraph)
            .Where(type => type.Namespace is { } value &&
                (value.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
                 value.StartsWith("Trading.Data", StringComparison.Ordinal) ||
                 value.StartsWith("System.Windows", StringComparison.Ordinal) ||
                 value.Contains("OpenAI", StringComparison.OrdinalIgnoreCase) ||
                 value.Contains("BrokerSdk", StringComparison.OrdinalIgnoreCase)) ||
                type.Name.StartsWith("IQueryable", StringComparison.Ordinal))
            .Select(type => type.FullName).Distinct().ToArray();
        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void EveryAsynchronousRuntimeOperationAcceptsCancellation()
    {
        var methods = Contracts.SelectMany(type => type.GetMethods())
            .Where(method => typeof(Task).IsAssignableFrom(method.ReturnType) ||
                             method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));
        Assert.That(methods, Has.All.Matches<MethodInfo>(method =>
            method.GetParameters().Any(parameter => parameter.ParameterType == typeof(CancellationToken))));
    }

    [Test]
    public void StageThreeProductionToolsAreExact() =>
        Assert.That(new[] { StageThreeTools.GetPortfolioSnapshot, StageThreeTools.Finish },
            Is.EquivalentTo(ExpectedToolNames));

    private static IEnumerable<Type> TypeGraph(Type type)
    {
        yield return type;
        foreach (var method in type.GetMethods())
        {
            yield return method.ReturnType;
            foreach (var parameter in method.GetParameters()) yield return parameter.ParameterType;
        }
    }
}
