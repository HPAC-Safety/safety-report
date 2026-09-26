using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests.Media;

/// <summary>
///     Minting an upload judges the declaration, then signs one PUT to one quarantine
///     key, and writes nothing (ADR-0126, REQ-SUB-072, REQ-SUB-073).
/// </summary>
public class UploadLinkTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

	private static readonly MediaPolicy Policy =
		new(new MediaSizeLimits(image: 100, video: 1_000, document: 200), MediaType.All);

	[Fact]
	public async Task GivenAcceptableDeclaration_WhenMinted_ThenOnePutToItsQuarantineKeyAndNothingWritten()
	{
		// Given
		var store = new InMemoryBlobStore();
		var link = new UploadLink(store, Policy, new FixedClock(Now));

		// When
		var minted = await link.Mint("Video/MP4; codecs=avc1", 1_000, CancellationToken.None);

		// Then
		minted.IsMinted.ShouldBeTrue();
		minted.Kind.ShouldBe(MediaKind.Video);
		minted.ExpiresAt.ShouldBe(Now.Add(BlobUrlLifetime.Maximum));
		var url = minted.Url!.ToString();
		url.ShouldContain($"quarantine/{minted.UploadId.Value}?op=put");
		url.ShouldContain("ct=video%2Fmp4&");
		url.ShouldContain("len=1000&");
		url.ShouldContain($"ttl={BlobUrlLifetime.Maximum.TotalSeconds}");
		store.Keys.ShouldBeEmpty();
	}

	[Fact]
	public async Task GivenTwoMints_WhenCompared_ThenEachHasItsOwnUploadId()
	{
		// Given
		var link = new UploadLink(new InMemoryBlobStore(), Policy, new FixedClock(Now));

		// When
		var first = await link.Mint("image/png", 10, CancellationToken.None);
		var second = await link.Mint("image/png", 10, CancellationToken.None);

		// Then
		first.UploadId.ShouldNotBe(second.UploadId);
	}

	[Theory]
	[InlineData("image/png", 0, MediaRejectionReason.Empty)]
	[InlineData("image/png", 101, MediaRejectionReason.TooLarge)]
	[InlineData("video/mp4", 1_001, MediaRejectionReason.TooLarge)]
	[InlineData("application/pdf", 201, MediaRejectionReason.TooLarge)]
	[InlineData("application/x-msdownload", 10, MediaRejectionReason.UnacceptedMediaType)]
	public async Task GivenRefusedDeclaration_WhenMinted_ThenNothingIsMinted(string declared,
																			 long byteSize,
																			 MediaRejectionReason reason)
	{
		// Given
		var store = new InMemoryBlobStore();
		var link = new UploadLink(store, Policy, new FixedClock(Now));

		// When
		var minted = await link.Mint(declared, byteSize, CancellationToken.None);

		// Then
		minted.IsMinted.ShouldBeFalse();
		minted.Url.ShouldBeNull();
		minted.RejectionReason.ShouldBe(reason);
		store.Keys.ShouldBeEmpty();
	}

	[Theory]
	[InlineData("image/png", "image/png")]
	[InlineData(" Text/Markdown ; charset=utf-8", "text/markdown")]
	public void GivenDeclaredType_WhenReducedToEssence_ThenLowerCaseWithoutParameters(string declared,
																					   string essence)
	{
		// Given / When / Then
		UploadLink.Essence(declared).ShouldBe(essence);
	}
}
