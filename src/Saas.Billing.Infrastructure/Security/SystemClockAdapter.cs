using Saas.Billing.Application.Ports;

namespace Saas.Billing.Infrastructure.Security;

internal sealed class SystemClockAdapter : IClockPort
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
