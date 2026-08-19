using System.Reflection;
using Trading.Core.Bots;
using Trading.Core.Brokers;
using Trading.Core.Persistence;
using Trading.Core.Portfolios;

namespace Trading.Architecture.Tests;

public sealed class PersistenceContractArchitectureTests
{
    private static readonly Type[] RepositoryContracts =
    [
        typeof(IBrokerConnectionRepository),
        typeof(IBrokerAccountRepository),
        typeof(IInstrumentRepository),
        typeof(ITradingBotRepository),
        typeof(IPortfolioRepository),
        typeof(IPositionRepository),
        typeof(IPortfolioLedgerRepository),
        typeof(IPortfolioDecisionSnapshotRepository),
    ];

    [Test]
    public void EveryStageTwoAggregateRootHasARepositoryContract()
    {
        Type[] aggregateRoots =
        [
            typeof(BrokerConnection), typeof(BrokerAccount), typeof(Instrument), typeof(TradingBot),
            typeof(Portfolio), typeof(Position), typeof(PortfolioLedgerEntry), typeof(PortfolioDecisionSnapshot),
        ];

        var returnedAggregateTypes = RepositoryContracts
            .SelectMany(contract => contract.GetMethods())
            .SelectMany(method => EnumerateTypeGraph(method.ReturnType))
            .Where(aggregateRoots.Contains)
            .ToHashSet();

        Assert.That(returnedAggregateTypes, Is.EquivalentTo(aggregateRoots));
    }

    [Test]
    public void MutableRepositoryWritesRequireAnExpectedVersion()
    {
        var mutableContracts = RepositoryContracts.Except(
            [typeof(IPortfolioLedgerRepository), typeof(IPortfolioDecisionSnapshotRepository)]);

        var violations = mutableContracts
            .Select(contract => contract.GetMethod("UpdateAsync"))
            .Where(method => method is null || method.GetParameters().All(parameter =>
                parameter.Name != "expectedVersion" || parameter.ParameterType != typeof(long)))
            .ToArray();

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void WriteContractsReturnExplicitProviderNeutralResults()
    {
        var writeMethods = RepositoryContracts
            .SelectMany(contract => contract.GetMethods())
            .Where(method => method.Name is "AddAsync" or "AppendAsync" or "PublishAsync" or "UpdateAsync")
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(writeMethods, Is.Not.Empty);
            Assert.That(writeMethods, Has.All.Property(nameof(MethodInfo.ReturnType))
                .EqualTo(typeof(Task<PersistenceWriteResult>)));
            Assert.That(typeof(PersistenceWriteResult.Succeeded).IsSealed, Is.True);
            Assert.That(typeof(PersistenceWriteResult.UniquenessConflict).IsSealed, Is.True);
            Assert.That(typeof(PersistenceWriteResult.ConcurrencyConflict).IsSealed, Is.True);
        });
    }

    [Test]
    public void PersistenceContractsDoNotExposeImplementationTypes()
    {
        var contractTypes = RepositoryContracts
            .Append(typeof(IUnitOfWork))
            .Append(typeof(IPortfolioQueries));

        var violations = contractTypes.SelectMany(contract => contract.GetMethods().SelectMany(method =>
                method.GetParameters().Select(parameter => (Member: $"{contract.Name}.{method.Name}", Type: parameter.ParameterType))
                    .Append((Member: $"{contract.Name}.{method.Name}", Type: method.ReturnType))))
            .SelectMany(item => EnumerateTypeGraph(item.Type).Select(type => (item.Member, Type: type)))
            .Where(item => IsForbidden(item.Type))
            .Select(item => $"{item.Member} exposes {item.Type.FullName}.")
            .ToArray();

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void PortfolioQueryContractsReturnImmutableProjections()
    {
        var projectionTypes = typeof(IPortfolioQueries).GetMethods()
            .SelectMany(method => EnumerateTypeGraph(method.ReturnType))
            .Where(type => type.Namespace == typeof(PortfolioSummary).Namespace && type.IsClass)
            .Distinct()
            .ToArray();

        var mutableProperties = projectionTypes
            .SelectMany(type => type.GetProperties().Where(property =>
                property.SetMethod is not null && !IsInitOnly(property)).Select(property => $"{type.Name}.{property.Name}"))
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(projectionTypes, Is.EquivalentTo(new[]
            {
                typeof(PortfolioSummary), typeof(PositionView), typeof(PortfolioLedgerEntryView),
                typeof(PortfolioDecisionSnapshotSummary), typeof(BrokerAccountAssociationView),
            }));
            Assert.That(mutableProperties, Is.Empty);
            Assert.That(typeof(IPortfolioQueries).GetMethods().Where(method => method.Name is not "GetSummaryAsync" and not "GetBrokerAccountAssociationAsync"),
                Has.All.Matches<MethodInfo>(method => EnumerateTypeGraph(method.ReturnType)
                    .Any(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))));
        });
    }

    private static IEnumerable<Type> EnumerateTypeGraph(Type type)
    {
        yield return type;
        if (type.HasElementType && type.GetElementType() is { } elementType)
        {
            foreach (var nested in EnumerateTypeGraph(elementType)) yield return nested;
        }

        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var nested in EnumerateTypeGraph(argument)) yield return nested;
        }
    }

    private static bool IsForbidden(Type type) =>
        type.Namespace?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true ||
        type.Namespace?.StartsWith("Trading.Data", StringComparison.Ordinal) == true ||
        type.Name.StartsWith("DbSet", StringComparison.Ordinal) ||
        (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IQueryable<>));

    private static bool IsInitOnly(PropertyInfo property) =>
        property.SetMethod!.ReturnParameter.GetRequiredCustomModifiers()
            .Contains(typeof(System.Runtime.CompilerServices.IsExternalInit));
}
