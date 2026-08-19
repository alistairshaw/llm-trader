using Microsoft.EntityFrameworkCore;

namespace Trading.Data;

public sealed class DatabaseInitializer(TradingDbContext dbContext)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Migration failures deliberately escape so host startup cannot continue with an unknown schema.
        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}
