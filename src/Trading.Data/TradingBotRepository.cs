using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Trading.Core.Bots;
using Trading.Core.FinancialValues;
using Trading.Core.Identifiers;
using Trading.Core.Persistence;
using Trading.Core.Policies;

namespace Trading.Data;

public sealed class TradingBotRepository(TradingDbContext dbContext) : ITradingBotRepository
{
    public async Task<TradingBot?> GetAsync(TradingBotId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        var entity = await dbContext.TradingBots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.ToString(), cancellationToken).ConfigureAwait(false);
        if (entity is null) return null;
        var versions = await dbContext.TradingBotConfigurationVersions.AsNoTracking()
            .Where(x => x.TradingBotId == entity.Id).OrderBy(x => x.VersionNumber).ToListAsync(cancellationToken).ConfigureAwait(false);
        return TradingBotMapper.ToDomain(entity, versions);
    }

    public async Task<PersistenceWriteResult> AddAsync(TradingBot bot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bot);
        var ownsTransaction = dbContext.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        try
        {
            var activeId = bot.ActiveConfigurationVersionId;
            var entity = TradingBotMapper.ToEntity(bot); entity.ActiveConfigurationVersionId = null;
            dbContext.TradingBots.Add(entity);
            dbContext.TradingBotConfigurationVersions.AddRange(bot.ConfigurationVersions.Select(x => TradingBotMapper.ToEntity(bot.Id, x)));
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            entity.ActiveConfigurationVersionId = activeId?.ToString();
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new PersistenceWriteResult.Succeeded();
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException
        { SqliteExtendedErrorCode: 1555 or 2067 })
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
            return new PersistenceWriteResult.UniquenessConflict("trading_bot_name_or_configuration_version");
        }
    }

    public async Task<PersistenceWriteResult> UpdateAsync(TradingBot bot, long expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bot);
        var entity = await dbContext.TradingBots.SingleOrDefaultAsync(x => x.Id == bot.Id.ToString(), cancellationToken).ConfigureAwait(false);
        if (entity is null || entity.Version != expectedVersion) return new PersistenceWriteResult.ConcurrencyConflict(expectedVersion, entity?.Version);
        var existing = await dbContext.TradingBotConfigurationVersions.Where(x => x.TradingBotId == entity.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var stored in existing)
        {
            var current = bot.ConfigurationVersions.SingleOrDefault(x => x.Id.ToString() == stored.Id);
            if (current is null || TradingBotMapper.ContentHash(current) != stored.ContentHash)
                throw new InvalidOperationException("Published configuration content is immutable.");
            TradingBotMapper.CopyLifecycle(current, stored);
        }
        var additions = bot.ConfigurationVersions.Where(x => existing.All(y => y.Id != x.Id.ToString())).ToArray();
        dbContext.TradingBotConfigurationVersions.AddRange(additions.Select(x => TradingBotMapper.ToEntity(bot.Id, x)));
        TradingBotMapper.Copy(bot, entity); entity.Version = expectedVersion + 1;
        return await RepositoryWrites.SaveAsync(dbContext, "trading_bot_name_or_configuration_version", cancellationToken).ConfigureAwait(false);
    }
}

internal static class TradingBotMapper
{
    private const int JsonSchemaVersion = 1;
    public static TradingBotEntity ToEntity(TradingBot bot) { var result = new TradingBotEntity(); Copy(bot, result); return result; }
    public static void Copy(TradingBot bot, TradingBotEntity entity)
    {
        entity.Id = bot.Id.ToString(); entity.Name = bot.Name; entity.Status = CanonicalEnumeration.Format(bot.Status);
        entity.ActiveConfigurationVersionId = bot.ActiveConfigurationVersionId?.ToString();
        entity.RequestedNextRunAt = bot.RequestedNextRunAt is null ? null : UtcUnixMilliseconds.ToProvider(bot.RequestedNextRunAt.Value);
        entity.AcceptedNextRunAt = bot.AcceptedNextRunAt is null ? null : UtcUnixMilliseconds.ToProvider(bot.AcceptedNextRunAt.Value);
        entity.LastCompletedRunId = bot.LastCompletedRunId?.ToString(); entity.CreatedAt = UtcUnixMilliseconds.ToProvider(bot.CreatedAt);
        entity.UpdatedAt = UtcUnixMilliseconds.ToProvider(bot.UpdatedAt); entity.Version = bot.Version;
    }
    public static TradingBotConfigurationVersionEntity ToEntity(TradingBotId botId, TradingBotConfigurationVersion version)
    {
        var entity = new TradingBotConfigurationVersionEntity
        {
            Id = version.Id.ToString(),
            TradingBotId = botId.ToString(),
            VersionNumber = version.VersionNumber,
            InvestmentMandateJson = CanonicalJsonSerializer.Serialize(JsonSchemaVersion, MandateDto.From(version.InvestmentMandate)),
            RiskPolicyJson = CanonicalJsonSerializer.Serialize(JsonSchemaVersion, RiskPolicyDto.From(version.RiskPolicy)),
            ToolPolicyJson = CanonicalJsonSerializer.Serialize(JsonSchemaVersion, ToolPolicyDto.From(version.ToolPolicy)),
            RunBudgetJson = CanonicalJsonSerializer.Serialize(JsonSchemaVersion, RunBudgetDto.From(version.RunBudget)),
            SchedulingPolicyJson = CanonicalJsonSerializer.Serialize(JsonSchemaVersion, version.SchedulingPolicy),
            ExecutionMode = CanonicalEnumeration.Format(version.ExecutionMode),
            ModelConfigurationJson = CanonicalJsonSerializer.Serialize(JsonSchemaVersion, version.ModelConfiguration),
            PromptVersion = version.PromptVersion,
            CreatedAt = UtcUnixMilliseconds.ToProvider(version.CreatedAt)
        };
        entity.ContentHash = ContentHash(version); CopyLifecycle(version, entity); return entity;
    }
    public static void CopyLifecycle(TradingBotConfigurationVersion version, TradingBotConfigurationVersionEntity entity)
    { entity.ActivatedAt = version.ActivatedAt is null ? null : UtcUnixMilliseconds.ToProvider(version.ActivatedAt.Value); entity.SupersededAt = version.SupersededAt is null ? null : UtcUnixMilliseconds.ToProvider(version.SupersededAt.Value); }
    public static string ContentHash(TradingBotConfigurationVersion version) => CanonicalJsonSerializer.Sha256(CanonicalJsonSerializer.Serialize(JsonSchemaVersion, new ConfigurationHashContent(
        version.InvestmentMandate, version.RiskPolicy, version.ToolPolicy, version.RunBudget, version.SchedulingPolicy, version.ExecutionMode, version.ModelConfiguration, version.PromptVersion)));
    public static TradingBot ToDomain(TradingBotEntity entity, IEnumerable<TradingBotConfigurationVersionEntity> versions) => TradingBot.Rehydrate(
        TradingBotId.Parse(entity.Id), entity.Name, CanonicalEnumeration.Parse<TradingBotStatus>(entity.Status), null,
        entity.ActiveConfigurationVersionId is null ? null : TradingBotConfigurationVersionId.Parse(entity.ActiveConfigurationVersionId),
        entity.RequestedNextRunAt is null ? null : UtcUnixMilliseconds.FromProvider(entity.RequestedNextRunAt.Value), entity.AcceptedNextRunAt is null ? null : UtcUnixMilliseconds.FromProvider(entity.AcceptedNextRunAt.Value),
        entity.LastCompletedRunId is null ? null : BotRunId.Parse(entity.LastCompletedRunId), UtcUnixMilliseconds.FromProvider(entity.CreatedAt), UtcUnixMilliseconds.FromProvider(entity.UpdatedAt), entity.Version,
        versions.Select(x => new TradingBotConfigurationVersionState(TradingBotConfigurationVersionId.Parse(x.Id), x.VersionNumber,
            CanonicalJsonSerializer.Deserialize<MandateDto>(JsonSchemaVersion, x.InvestmentMandateJson).ToDomain(), CanonicalJsonSerializer.Deserialize<RiskPolicyDto>(JsonSchemaVersion, x.RiskPolicyJson).ToDomain(),
            CanonicalJsonSerializer.Deserialize<ToolPolicyDto>(JsonSchemaVersion, x.ToolPolicyJson).ToDomain(), CanonicalJsonSerializer.Deserialize<RunBudgetDto>(JsonSchemaVersion, x.RunBudgetJson).ToDomain(),
            CanonicalJsonSerializer.Deserialize<SchedulingPolicy>(JsonSchemaVersion, x.SchedulingPolicyJson), CanonicalEnumeration.Parse<ExecutionMode>(x.ExecutionMode),
            CanonicalJsonSerializer.Deserialize<ModelConfiguration>(JsonSchemaVersion, x.ModelConfigurationJson), x.PromptVersion, UtcUnixMilliseconds.FromProvider(x.CreatedAt),
            x.ActivatedAt is null ? null : UtcUnixMilliseconds.FromProvider(x.ActivatedAt.Value), x.SupersededAt is null ? null : UtcUnixMilliseconds.FromProvider(x.SupersededAt.Value))));
    private sealed record ConfigurationHashContent(InvestmentMandate InvestmentMandate, RiskPolicy RiskPolicy, ToolPolicy ToolPolicy, RunBudget RunBudget,
        SchedulingPolicy SchedulingPolicy, ExecutionMode ExecutionMode, ModelConfiguration ModelConfiguration, string PromptVersion);
    private sealed record MandateDto(string Objective, long InvestmentHorizonTicks, string[] AssetClasses, string[] Markets, string[] Currencies)
    {
        public static MandateDto From(InvestmentMandate value) => new(value.Objective, value.InvestmentHorizon.Ticks, value.Universe.AssetClasses.ToArray(), value.Universe.Markets.ToArray(), value.Universe.Currencies.Select(x => x.Code).ToArray());
        public InvestmentMandate ToDomain() => new(Objective, TimeSpan.FromTicks(InvestmentHorizonTicks), new UniverseDefinition(AssetClasses, Markets, Currencies.Select(x => new Currency(x))));
    }
    private sealed record RiskLimitDto(string Metric, decimal Minimum, decimal Maximum, string Unit);
    private sealed record RiskPolicyDto(RiskLimitDto[] Limits, bool TradingHalted)
    {
        public static RiskPolicyDto From(RiskPolicy value) => new(value.Limits.Select(x => new RiskLimitDto(x.Metric, x.Minimum, x.Maximum, x.Unit)).ToArray(), value.TradingHalted);
        public RiskPolicy ToDomain() => new(Limits.Select(x => new RiskLimit(x.Metric, x.Maximum, x.Unit, x.Minimum)), TradingHalted);
    }
    private sealed record ToolAllowanceDto(string ToolName, int CallLimit);
    private sealed record ToolPolicyDto(ToolAllowanceDto[] AllowedTools)
    {
        public static ToolPolicyDto From(ToolPolicy value) => new(value.AllowedTools.Select(x => new ToolAllowanceDto(x.ToolName, x.CallLimit)).ToArray());
        public ToolPolicy ToDomain() => new(AllowedTools.Select(x => new ToolAllowance(x.ToolName, x.CallLimit)));
    }
    private sealed record RunBudgetDto(long WallClockTicks, long TokenLimit, decimal CostAmount, string CostCurrency, int ToolCallLimit, int ResearchRequestLimit, int ProposalLimit)
    {
        public static RunBudgetDto From(RunBudget value) => new(value.WallClock.Ticks, value.TokenLimit, value.CostLimit.Amount, value.CostLimit.Currency.Code, value.ToolCallLimit, value.ResearchRequestLimit, value.ProposalLimit);
        public RunBudget ToDomain() => new(TimeSpan.FromTicks(WallClockTicks), TokenLimit, new Money(CostAmount, new Currency(CostCurrency)), ToolCallLimit, ResearchRequestLimit, ProposalLimit);
    }
}
