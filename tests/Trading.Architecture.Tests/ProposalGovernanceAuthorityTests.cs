using System.Reflection;
using Trading.Core.Proposals;
using Trading.Engine.Proposals;
using Trading.Engine.Runtime;

namespace Trading.Architecture.Tests;

public sealed class ProposalGovernanceAuthorityTests
{
    private static readonly Type[] GovernancePorts =
    [
        typeof(IProposalRecorder), typeof(IProposalRecordingContextProvider), typeof(IGuardrailPolicyEvaluator),
        typeof(IProposalDecisionAuthorizer), typeof(IFreshProposalStateProvider), typeof(ICapitalAvailabilityProvider),
        typeof(ICapitalReservationService), typeof(IProposalGovernanceClock),
        typeof(IProposalGovernanceIdentifierSource), typeof(IProposalGovernanceTransaction),
    ];

    [Test]
    public void GovernanceContractsExposeNoBrokerOrOrderSubmissionAuthority()
    {
        var exposed = GovernancePorts.SelectMany(TypeGraph).Where(type =>
            type.Namespace?.StartsWith("Trading.Core.Brokers", StringComparison.Ordinal) == true ||
            type.Namespace?.StartsWith("Trading.Brokers", StringComparison.Ordinal) == true ||
            type.Name.Contains("Broker", StringComparison.Ordinal) ||
            type.Name.Contains("SubmitOrder", StringComparison.Ordinal)).Select(type => type.FullName).Distinct();

        Assert.That(exposed, Is.Empty);
    }

    [Test]
    public void CoreProposalContractsRemainInfrastructureNeutral()
    {
        var assemblyReferences = typeof(TradeProposal).Assembly.GetReferencedAssemblies().Select(reference => reference.Name);
        Assert.That(assemblyReferences, Has.None.Matches<string>(name =>
            name is not null && (name.Contains("EntityFrameworkCore", StringComparison.Ordinal) ||
                                 name.Contains("Sqlite", StringComparison.Ordinal) ||
                                 name.Contains("WindowsDesktop", StringComparison.Ordinal))));
    }

    [Test]
    public void ProposalToolDispatcherHasNoBrokerAssemblyReferenceOrSubmissionSurface()
    {
        var assemblyReferences = typeof(ProposalToolDispatcher).Assembly.GetReferencedAssemblies().Select(x => x.Name);
        var tools = ProposalToolDispatcher.Definitions.Select(x => x.Name);
        Assert.Multiple(() =>
        {
            Assert.That(assemblyReferences, Does.Not.Contain("Trading.Brokers"));
            Assert.That(tools, Has.None.Matches<string>(x => x.Contains("Submit", StringComparison.Ordinal) || x.Contains("Broker", StringComparison.Ordinal)));
        });
    }

    [Test, Category("ResearchOnlyProposal")]
    public void ResearchOnlyGovernanceServicesHaveNoOrderOrBrokerAuthority()
    {
        Type[] researchOnlyPath =
        [
            typeof(ProposalToolDispatcher), typeof(GuardrailEvaluationService),
            typeof(HumanProposalDecisionService), typeof(CapitalReservationService)
        ];
        var dependencies = researchOnlyPath.SelectMany(TypeGraph).Select(x => x.FullName ?? x.Name).Distinct();
        Assert.That(dependencies, Has.None.Matches<string>(name =>
            name.Contains("Trading.Brokers", StringComparison.Ordinal) ||
            name.Contains("SubmitOrder", StringComparison.Ordinal) ||
            name.Contains("IOrderRepository", StringComparison.Ordinal)));
    }

    private static IEnumerable<Type> TypeGraph(Type root)
    {
        yield return root;
        foreach (var method in root.GetMethods())
        {
            yield return Unwrap(method.ReturnType);
            foreach (var parameter in method.GetParameters()) yield return Unwrap(parameter.ParameterType);
        }
    }

    private static Type Unwrap(Type type) => type.IsGenericType ? type.GetGenericArguments().Last() : type;
}
