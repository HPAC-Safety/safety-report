using System.Text.Json;
using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests.Media;

/// <summary>
/// English and French are both first-class here, so a rejection is a key that the
/// edge renders, never a sentence baked into the domain. These tests are what
/// stop the two drifting: every reason has a key, and every key has English
/// wording in <c>locales/en-CA.json</c>. See AGENTS.md and docs/localization.md.
/// </summary>
public class MediaRejectionTests
{
    [Fact]
    public void Given_every_rejection_reason_When_its_key_is_requested_Then_one_is_returned()
    {
        // Given
        var reasons = Enum.GetValues<MediaRejectionReason>().Where(r => r is not MediaRejectionReason.None);

        // When
        var keys = reasons.Select(MediaRejection.LocalizationKeyFor).ToArray();

        // Then
        keys.ShouldAllBe(key => key.StartsWith(MediaRejection.KeyPrefix, StringComparison.Ordinal));
        keys.Distinct().Count().ShouldBe(keys.Length);
    }

    [Fact]
    public void Given_an_accepted_upload_When_a_rejection_key_is_requested_Then_it_throws()
    {
        // Given / When / Then
        // There is nothing to tell the reporter, and returning a key for "None"
        // would let a caller render a rejection for a file that was accepted.
        Should.Throw<ArgumentOutOfRangeException>(() => MediaRejection.LocalizationKeyFor(MediaRejectionReason.None));
    }

    [Fact]
    public void Given_every_rejection_reason_When_the_english_locale_is_read_Then_each_key_has_wording()
    {
        // Given
        var locale = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(ReviewerLinkIsTheOnlyChokepointTests.RepositoryRoot(), "locales", "en-CA.json")));
        locale.ShouldNotBeNull();

        // When
        var missing = Enum.GetValues<MediaRejectionReason>()
            .Where(r => r is not MediaRejectionReason.None)
            .Select(MediaRejection.LocalizationKeyFor)
            .Where(key => !locale.ContainsKey(key))
            .ToArray();

        // Then
        // A reason with no English wording reaches a reporter as a raw key name.
        missing.ShouldBeEmpty();
    }
}
