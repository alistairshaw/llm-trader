using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Research.Contracts;

namespace Trading.Research.Tests;

[Category("Orchestration")]
[Category("Recovery")]
public sealed class ResearchOrchestrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 22, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task RecoveryProcessesEachOrphanOnceAndHonorsCancellation()
    {
        var repository = new RecoveryRepository([ResearchRunAttemptId.New(), ResearchRunAttemptId.New()]);
        var service = new ResearchRestartRecovery(repository, new Clock(), Defaults());
        Assert.That(await service.RecoverAsync(default), Is.EqualTo(2));
        Assert.That(repository.Recovered, Has.Count.EqualTo(2));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.That(async () => await service.RecoverAsync(cancelled.Token), Throws.TypeOf<OperationCanceledException>());
    }

    [Test]
    public void DurableAuditReconstructsExactDraftAndRetrievedProvenance()
    {
        var attempt = ResearchRunAttemptId.New(); var hash = new string('a', 64);
        var fetch = new ResearchToolAudit("f", attempt, 1, StageFourResearchTools.FetchWebDocument, 1, "{}", "Succeeded", Now, Now,
            $"{{\"document\":{{\"contentHash\":\"{hash}\",\"provider\":\"approved-fixtures\",\"publishedAt\":null,\"retrievedAt\":\"2026-08-20T22:00:00.000Z\",\"sourceIdentifier\":\"fixture://one\"}}}}", null, null, "{}");
        var args = $"{{\"attemptId\":\"{attempt}\",\"citations\":[{{\"contentHash\":\"{hash}\",\"provider\":\"approved-fixtures\",\"publishedAt\":null,\"retrievedAt\":\"2026-08-20T22:00:00.000Z\",\"sourceIdentifier\":\"fixture://one\"}}],\"content\":{{\"schemaVersion\":1}},\"dataCutoff\":\"2026-08-20T20:00:00.000Z\",\"recommendedRefreshAt\":null}}";
        var draft = new ResearchToolAudit("d", attempt, 2, StageFourResearchTools.PublishReportDraft, 1, args, "Succeeded", Now, Now, "{}", null, null, "{}");
        var reconstructed = ResearchRunOrchestrator.Reconstruct([fetch, draft]);
        Assert.Multiple(() => { Assert.That(reconstructed.Draft.CanonicalContent, Is.EqualTo("{\"schemaVersion\":1}")); Assert.That(reconstructed.Sources.Single().SourceIdentifier, Is.EqualTo("fixture://one")); Assert.That(reconstructed.Draft.Citations, Is.EqualTo(reconstructed.Sources)); });
    }

    private static ResearchRunDefaults Defaults() => new(new("scripted", "research", "1", "p", "t", "1"),
        new(TimeSpan.FromMinutes(1), 1, new Trading.Core.FinancialValues.Money(1, Trading.Core.FinancialValues.Currency.USD), 1, 1, 1, 1));
    private sealed class Clock : IResearchClock { public DateTimeOffset UtcNow => Now; }
    private sealed class RecoveryRepository(IReadOnlyList<ResearchRunAttemptId> values) : IResearchOrchestrationRepository
    {
        public List<ResearchRunAttemptId> Recovered { get; } = [];
        public Task<IReadOnlyList<ResearchRunAttemptId>> GetOrphanedAsync(DateTimeOffset before, int limit, CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.FromResult(values); }
        public Task<PersistenceWriteResult> RecoverAndRequeueAsync(ResearchRunAttemptId id, DateTimeOffset at, string code, CancellationToken token) { Recovered.Add(id); return Task.FromResult<PersistenceWriteResult>(new PersistenceWriteResult.Succeeded()); }
        public Task<IReadOnlyList<ResearchRequestId>> GetQueuedAsync(int limit, CancellationToken token) => throw new NotSupportedException();
        public Task<ResearchOrchestrationWork?> TryClaimAsync(ResearchRequestId id, Trading.Core.Research.ResearchRunAttempt attempt, CancellationToken token) => throw new NotSupportedException();
        public Task<PersistenceWriteResult> TerminalizeAsync(ResearchRequestId id, Trading.Core.Research.ResearchRunAttempt attempt, Trading.Core.Research.ResearchRequestStatus status, long version, CancellationToken token) => throw new NotSupportedException();
    }
}
