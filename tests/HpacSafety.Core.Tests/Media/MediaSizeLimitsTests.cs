using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests.Media;

/// <summary>One size limit per kind (ADR-0126).</summary>
public class MediaSizeLimitsTests
{
	[Fact]
	public void GivenLimitsPerKind_WhenAskedForEachKind_ThenEachKindsOwnLimit()
	{
		// Given
		var limits = new MediaSizeLimits(image: 1, video: 3, document: 2);

		// When / Then
		limits.For(MediaKind.Image).ShouldBe(1);
		limits.For(MediaKind.Video).ShouldBe(3);
		limits.For(MediaKind.Document).ShouldBe(2);
		limits.Largest.ShouldBe(3);
	}

	[Fact]
	public void GivenUndefinedKind_WhenAskedForItsLimit_ThenThrows()
	{
		// Given
		var limits = MediaSizeLimits.Uniform(5);

		// When / Then
		Should.Throw<ArgumentOutOfRangeException>(() => limits.For((MediaKind)99));
	}

	[Theory]
	[InlineData(0, 1, 1)]
	[InlineData(1, 0, 1)]
	[InlineData(1, 1, -1)]
	public void GivenLimitNotAboveZero_WhenCreated_ThenThrows(long image,
															  long video,
															  long document)
	{
		// Given / When / Then
		Should.Throw<ArgumentOutOfRangeException>(() => new MediaSizeLimits(image, video, document));
	}
}
