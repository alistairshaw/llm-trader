using Microsoft.EntityFrameworkCore.Storage;
using Trading.Core.Bots;
using Trading.Core.Persistence;
using Trading.Core.Portfolios;

namespace Trading.Data;

public sealed class Stage2TransactionOperations
{
    private readonly TradingDbContext _context;
    private readonly Action<TransactionFailpoint>? _failpoint;

    public Stage2TransactionOperations(TradingDbContext context) : this(context, null) { }

    internal Stage2TransactionOperations(TradingDbContext context, Action<TransactionFailpoint>? failpoint)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _failpoint = failpoint;
    }

    public Task<PersistenceWriteResult> CreateBotAsync(TradingBot bot, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => await new TradingBotRepository(_context).AddAsync(bot, cancellationToken).ConfigureAwait(false), cancellationToken);

    public Task<PersistenceWriteResult> AssignPortfolioOwnershipAsync(Portfolio portfolio, long expectedVersion, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => await new PortfolioRepository(_context).UpdateAsync(portfolio, expectedVersion, cancellationToken).ConfigureAwait(false), cancellationToken);

    public Task<PersistenceWriteResult> ApplyPositionFillAsync(Position position, long expectedVersion, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => await new PositionRepository(_context).UpdateAsync(position, expectedVersion, cancellationToken).ConfigureAwait(false), cancellationToken);

    public Task<PersistenceWriteResult> AppendLedgerEntryAsync(PortfolioLedgerEntry entry, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => await new PortfolioLedgerRepository(_context).AppendAsync(entry, cancellationToken).ConfigureAwait(false), cancellationToken);

    public Task<PersistenceWriteResult> AppendLedgerCorrectionAsync(PortfolioLedgerEntry correction, CancellationToken cancellationToken) =>
        AppendLedgerEntryAsync(correction, cancellationToken);

    public Task<PersistenceWriteResult> CreateDecisionSnapshotAsync(PortfolioDecisionSnapshot snapshot, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => await new PortfolioDecisionSnapshotRepository(_context).PublishAsync(snapshot, cancellationToken).ConfigureAwait(false), cancellationToken);

    private async Task<PersistenceWriteResult> ExecuteAsync(Func<Task<PersistenceWriteResult>> write, CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await write().ConfigureAwait(false);
            if (result is not PersistenceWriteResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return result;
            }

            _failpoint?.Invoke(TransactionFailpoint.AfterMaterialWrite);
            _failpoint?.Invoke(TransactionFailpoint.BeforeCommit);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _context.ChangeTracker.Clear();
            throw;
        }
    }
}

internal enum TransactionFailpoint
{
    AfterMaterialWrite,
    BeforeCommit
}
