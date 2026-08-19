using Microsoft.EntityFrameworkCore.Design;

namespace Trading.Data;

public sealed class TradingDbContextDesignTimeFactory : IDesignTimeDbContextFactory<TradingDbContext>
{
    public TradingDbContext CreateDbContext(string[] args) =>
        new(TradingDbContextFactory.CreateOptions(
            new DatabaseOptions { DatabasePath = Path.Combine(Path.GetTempPath(), "trading-design-time.db") },
            Directory.GetCurrentDirectory()));
}
