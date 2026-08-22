using System.Security.Cryptography;
using System.Text;
using Trading.Core.Brokers;
using Trading.Core.Persistence;

namespace Trading.Engine.Execution;

public sealed class ProposalOrderConversionService(
    IAtomicOrderConversionRepository repository,
    IOrderExecutionIdentifierSource identifiers) : IOrderConversionService
{
    public async Task<OrderConversionResult> ConvertAsync(
        OrderConversionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.RequestedAt.Offset != TimeSpan.Zero)
            throw new ArgumentException("Conversion time must be UTC.", nameof(command));

        var clientOrderId = DeriveClientOrderId(command.ProposalId.ToString());
        var result = await repository.TryConvertAsync(new AtomicOrderConversionRequest(
            command.ProposalId, command.ReservationId, identifiers.NewOrderId(),
            identifiers.NewWorkItemId(), identifiers.NewCorrelationId(), clientOrderId,
            command.RequestedAt), cancellationToken).ConfigureAwait(false);

        return result switch
        {
            AtomicOrderConversionWriteResult.Created created =>
                new(OrderConversionOutcome.Created, OrderConversionCodes.Created, created.Order),
            AtomicOrderConversionWriteResult.AlreadyCreated existing =>
                new(OrderConversionOutcome.AlreadyCreated, OrderConversionCodes.AlreadyCreated, existing.Order),
            AtomicOrderConversionWriteResult.Rejected rejected =>
                new(OrderConversionOutcome.Rejected, rejected.Code, null),
            AtomicOrderConversionWriteResult.NotFound =>
                new(OrderConversionOutcome.NotFound, OrderConversionCodes.NotFound, null),
            AtomicOrderConversionWriteResult.Contention =>
                new(OrderConversionOutcome.Contention, OrderConversionCodes.Contention, null),
            _ => throw new InvalidOperationException("Unknown order conversion result."),
        };
    }

    public static ClientOrderIdentity DeriveClientOrderId(string proposalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(proposalId);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"paper-order:v1:{proposalId.Trim()}"))).ToLowerInvariant();
        return new ClientOrderIdentity($"paper-{hash[..48]}");
    }
}
