using System.Reflection;
using Trading.Engine.Operators;

namespace Trading.Architecture.Tests;

[TestFixture, Category("OperatorContracts")]
public sealed class OperatorContractArchitectureTests
{
    private static readonly Type[] Contracts =
    [
        typeof(IOperatorQueries), typeof(IBotOperatorService), typeof(IRunOperatorService),
        typeof(IResearchOperatorService), typeof(IProposalOperatorService), typeof(IKillSwitchOperatorService),
        typeof(IOperatorAuthorization), typeof(IOperatorWorkflowPort),
    ];

    [Test]
    public void OperatorBoundaryIsUiInfrastructureAndBrokerSdkNeutral()
    {
        var violations = Contracts.SelectMany(TypeGraph).Where(type =>
            type.Namespace?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true ||
            type.Namespace?.StartsWith("Trading.Data", StringComparison.Ordinal) == true ||
            type.Namespace?.StartsWith("System.Windows", StringComparison.Ordinal) == true ||
            type.Name.StartsWith("IQueryable", StringComparison.Ordinal) ||
            type.Name.Contains("BrokerSdk", StringComparison.OrdinalIgnoreCase)).Select(type => type.FullName).Distinct();
        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void EveryOperatorOperationIsAsynchronousAndCancellable()
    {
        var methods = Contracts.SelectMany(type => type.GetMethods()).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(methods, Has.All.Matches<MethodInfo>(method => typeof(Task).IsAssignableFrom(method.ReturnType)));
            Assert.That(methods, Has.All.Matches<MethodInfo>(method => method.GetParameters()
                .Any(parameter => parameter.ParameterType == typeof(CancellationToken))));
        });
    }

    private static IEnumerable<Type> TypeGraph(Type root)
    {
        yield return root;
        foreach (var method in root.GetMethods())
        {
            yield return method.ReturnType;
            foreach (var parameter in method.GetParameters()) yield return parameter.ParameterType;
        }
    }
}
