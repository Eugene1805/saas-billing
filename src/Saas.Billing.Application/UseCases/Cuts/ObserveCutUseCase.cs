using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.Common;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.UseCases.Cuts;

public sealed class ObserveCutUseCase(
    ITenantBillingAccessPort tenantPort,
    ISpecialistCutRepository cutRepository,
    IPaymentRepository paymentRepository,
    IBillingAdjustmentRepository adjustmentRepository,
    IClockPort clock)
{
    public Task<Result<SpecialistCutDetailDto>> ExecuteAsync(
        BillingActorContext actor,
        Guid cutId,
        ResolveCutCommand command,
        CancellationToken cancellationToken = default) =>
        CutResolutionHelper.ResolveAsync(
            actor,
            cutId,
            isObservation: true,
            command.Note,
            tenantPort,
            cutRepository,
            paymentRepository,
            adjustmentRepository,
            clock,
            cancellationToken);
}
