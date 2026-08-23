using HpacSafety.Core.SharedKernel;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
/// There are no public object URLs, ever. Every URL this system hands out is
/// pre-signed and short-lived, and the cap is a domain rule rather than a
/// per-adapter setting so that no implementation can quietly widen it.
/// See docs/data-handling.md and ADR-0026.
/// </summary>
public class BlobUrlLifetimeTests
{
    [Fact]
    public void Given_a_lifetime_within_the_cap_When_it_is_validated_Then_it_is_returned_unchanged()
    {
        // Given
        var lifetime = TimeSpan.FromMinutes(5);

        // When
        var validated = BlobUrlLifetime.Validate(lifetime);

        // Then
        validated.ShouldBe(lifetime);
    }

    [Fact]
    public void Given_a_lifetime_beyond_the_cap_When_it_is_validated_Then_it_is_refused()
    {
        // Given
        var lifetime = BlobUrlLifetime.Maximum + TimeSpan.FromSeconds(1);

        // When / Then
        Should.Throw<DomainRuleViolationException>(() => BlobUrlLifetime.Validate(lifetime));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Given_a_lifetime_that_never_expires_or_has_expired_When_it_is_validated_Then_it_is_refused(int seconds)
    {
        // Given
        var lifetime = TimeSpan.FromSeconds(seconds);

        // When / Then
        Should.Throw<DomainRuleViolationException>(() => BlobUrlLifetime.Validate(lifetime));
    }

    [Fact]
    public void Given_the_cap_When_it_is_read_Then_it_is_measured_in_minutes_not_hours()
    {
        // Given / When
        var maximum = BlobUrlLifetime.Maximum;

        // Then
        maximum.ShouldBeLessThanOrEqualTo(TimeSpan.FromMinutes(15));
    }
}
