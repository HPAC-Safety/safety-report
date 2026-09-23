using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     An upload id is the only handle anybody has on an unclaimed upload, so it is
///     a capability: long, random, and nothing else (ADR-0096).
/// </summary>
public class UploadIdTests
{
	[Fact]
	public void GivenNewUploadId_WhenMinted_ThenTwentyTwoUrlSafeCharacters()
	{
		// Given / When
		var id = UploadId.New();

		// Then
		id.Value.Length.ShouldBe(UploadId.Length);
		id.Value.ShouldAllBe(character => TinyId.Alphabet.Contains(character));
	}

	[Fact]
	public void GivenManyUploadIds_WhenMinted_ThenNoneRepeat()
	{
		// Given / When
		var ids = Enumerable.Range(0, 1000).Select(_ => UploadId.New().Value).ToHashSet();

		// Then
		ids.Count.ShouldBe(1000);
	}

	[Fact]
	public void GivenMintedUploadId_WhenParsed_ThenRoundTrips()
	{
		// Given
		var id = UploadId.New();

		// When
		var parsed = UploadId.TryParse(id.Value, out var roundTripped);

		// Then
		parsed.ShouldBeTrue();
		roundTripped.ShouldBe(id);
		roundTripped.ToString().ShouldBe(id.Value);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("dQw4w9WgXcQ")]
	[InlineData("kP3x9QmR2vT8wLb6nYc4D")]
	[InlineData("kP3x9QmR2vT8wLb6nYc4Dgx")]
	[InlineData("kP3x9QmR2vT8wLb6nYc4D/")]
	[InlineData("kP3x9QmR2vT8wLb6nYc4D=")]
	public void GivenTextThatIsNotUploadId_WhenParsed_ThenRefused(string? candidate)
	{
		// Given / When
		var parsed = UploadId.TryParse(candidate, out var id);

		// Then
		parsed.ShouldBeFalse();
		id.Value.ShouldBeEmpty();
	}
}
