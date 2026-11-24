namespace Saas.Billing.Application.Ports;

public interface IClockPort
{
    DateTimeOffset UtcNow { get; }
}
