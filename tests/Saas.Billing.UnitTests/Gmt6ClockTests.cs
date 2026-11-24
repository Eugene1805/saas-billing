using Saas.Billing.Domain.Common;

namespace Saas.Billing.UnitTests;

public sealed class Gmt6ClockTests
{
    [Theory]
    [InlineData("2026-11-22T10:00:00-06:00", 16)]
    [InlineData("2026-11-22T23:30:00-06:00", 5)]
    public void TryParseStrictGmt6_AcceptsValidOffset_AndNormalizesToUtc(string input, int expectedUtcHour)
    {
        var ok = Gmt6Clock.TryParseStrictGmt6(input, out var parsedUtc);

        Assert.True(ok);
        Assert.Equal(TimeSpan.Zero, parsedUtc.Offset);
        Assert.Equal(expectedUtcHour, parsedUtc.Hour);
    }

    [Theory]
    [InlineData("")]
    [InlineData("2026-11-22T10:00:00Z")]
    [InlineData("2026-11-22T10:00:00-05:00")]
    [InlineData("2026-11-22")]
    public void TryParseStrictGmt6_RejectsNonGmt6(string input)
    {
        Assert.False(Gmt6Clock.TryParseStrictGmt6(input, out _));
    }

    [Fact]
    public void ToUtcDayBounds_MapsFullGmt6DayToUtcHalfOpenRange()
    {
        var day = new DateOnly(2026, 11, 22);
        var (startUtc, endExclusiveUtc) = Gmt6Clock.ToUtcDayBounds(day, day);

        Assert.Equal(new DateTimeOffset(2026, 11, 22, 6, 0, 0, TimeSpan.Zero), startUtc);
        Assert.Equal(new DateTimeOffset(2026, 11, 23, 6, 0, 0, TimeSpan.Zero), endExclusiveUtc);
    }
}
