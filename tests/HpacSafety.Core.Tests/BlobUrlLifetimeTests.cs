using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     There are no public object URLs, ever. Every URL this system hands out is
///     pre-signed and short-lived, and the cap is a domain rule rather than a
///     per-adapter setting so that no implementation can quietly widen it.
///     See docs/data-handling.md and ADR-0026.
/// </summary>
public class BlobUrlLifetimeTests
{
    [Fact]
    public void GivenLifetimeWithinCap_WhenValidated_ThenReturnedUnchanged()
    {
        // Given
        var lifetime = TimeSpan.FromMinutes(5);

        // When
        var validated = BlobUrlLifetime.Validate(lifetime);

        // Then
        validated.ShouldBe(lifetime);
    }

    [Fact]
    public void GivenLifetimeBeyondCap_WhenValidated_ThenRefused()
    {
        // Given
        var lifetime = BlobUrlLifetime.Maximum + TimeSpan.FromSeconds(1);

        // When / Then
        Should.Throw<DomainRuleViolationException>(() => BlobUrlLifetime.Validate(lifetime));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GivenLifetimeNeverExpiresOrHasExpired_WhenValidated_ThenRefused(int seconds)
    {
        // Given
        var lifetime = TimeSpan.FromSeconds(seconds);

        // When / Then
        Should.Throw<DomainRuleViolationException>(() => BlobUrlLifetime.Validate(lifetime));
    }

    [Fact]
    public void GivenCap_WhenRead_ThenMeasuredInMinutesNotHours()
    {
        // Given / When
        var maximum = BlobUrlLifetime.Maximum;

        // Then
        maximum.ShouldBeLessThanOrEqualTo(TimeSpan.FromMinutes(15));
    }
}
