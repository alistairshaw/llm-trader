using Trading.Core.Persistence;

namespace Trading.Data;

public sealed class EfUnitOfWork(TradingDbContext dbContext) : IUnitOfWork
{
    public async Task CommitAsync(CancellationToken cancellationToken) =>
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
}
