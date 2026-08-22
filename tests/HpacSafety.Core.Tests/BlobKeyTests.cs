using HpacSafety.Core.SharedKernel;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
/// A blob key is the only thing standing between an attacker-supplied string and
/// the filesystem in <c>FileSystemBlobStore</c>, so it is a value object with
/// rules rather than a <c>string</c>. See ADR-0026.
/// </summary>
public class BlobKeyTests
{
    [Fact]
    public void Given_a_conventional_key_When_it_is_parsed_Then_it_round_trips()
    {
        // Given
        const string candidate = "reports/9f1c/original/photo.jpg";

        // When
        var key = BlobKey.Parse(candidate);

        // Then
        key.Value.ShouldBe(candidate);
        key.ToString().ShouldBe(candidate);
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("reports/../../etc/passwd")]
    [InlineData("reports/./photo.jpg")]
    [InlineData("/reports/photo.jpg")]
    [InlineData("reports/photo.jpg/")]
    [InlineData("reports//photo.jpg")]
    [InlineData("reports\\photo.jpg")]
    [InlineData("C:/reports/photo.jpg")]
    [InlineData("reports/photo .jpg")]
    [InlineData("reports/pho\nto.jpg")]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_a_key_that_could_escape_its_prefix_When_it_is_parsed_Then_it_is_refused(string candidate)
    {
        // Given / When
        var parsed = BlobKey.TryParse(candidate, out _);

        // Then
        parsed.ShouldBeFalse();
        Should.Throw<DomainRuleViolationException>(() => BlobKey.Parse(candidate));
    }

    [Fact]
    public void Given_a_key_longer_than_the_limit_When_it_is_parsed_Then_it_is_refused()
    {
        // Given
        var candidate = new string('a', BlobKey.MaxLength + 1);

        // When
        var parsed = BlobKey.TryParse(candidate, out _);

        // Then
        parsed.ShouldBeFalse();
    }

    [Fact]
    public void Given_an_original_key_When_a_prefix_is_applied_Then_the_derivative_lives_under_it()
    {
        // Given
        var original = BlobKey.Parse("reports/9f1c/photo.jpg");

        // When
        var derivative = original.WithPrefix("stripped");

        // Then
        derivative.Value.ShouldBe("stripped/reports/9f1c/photo.jpg");
        derivative.ShouldNotBe(original);
    }

    [Fact]
    public void Given_a_hostile_prefix_When_it_is_applied_Then_it_is_refused()
    {
        // Given
        var original = BlobKey.Parse("reports/photo.jpg");

        // When / Then
        Should.Throw<DomainRuleViolationException>(() => original.WithPrefix(".."));
    }
}
