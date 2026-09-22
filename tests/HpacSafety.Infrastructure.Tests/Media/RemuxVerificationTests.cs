using System.Text.Json;
using HpacSafety.Infrastructure.Media;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Media;

/// <summary>
///     What makes a remuxed file a derivative rather than a repackaged original.
///     Read straight from ffprobe's output, so every refusal can be provoked here
///     instead of by hunting for a video that happens to trigger it.
/// </summary>
public sealed class RemuxVerificationTests
{
	private static JsonElement Probe(string json)
	{
		return JsonDocument.Parse(json).RootElement.Clone();
	}

	[Fact]
	public void GivenVideoAndAudioWithStructuralTags_WhenVerified_ThenAccepted()
	{
		// Given — what a clean remux actually produces
		var probe = Probe("""
			{
			  "streams": [
			    { "codec_type": "video", "tags": { "language": "und", "handler_name": "VideoHandler" } },
			    { "codec_type": "audio", "tags": { "language": "und", "handler_name": "SoundHandler" } }
			  ],
			  "format": { "tags": { "major_brand": "isom", "minor_version": "512", "compatible_brands": "isomiso2avc1mp41" } }
			}
			""");

		// When / Then
		RemuxVerification.Reject(probe).ShouldBeNull();
	}

	[Fact]
	public void GivenTimedMetadataTrack_WhenVerified_ThenRefused()
	{
		// Given — an iPhone's `mebx` track, which a tag-based wipe leaves behind
		var probe = Probe("""
			{ "streams": [ { "codec_type": "video" }, { "codec_type": "data" } ] }
			""");

		// When
		var refusal = RemuxVerification.Reject(probe);

		// Then
		refusal.ShouldNotBeNull();
		refusal.ShouldContain("data");
	}

	[Fact]
	public void GivenSubtitleTrack_WhenVerified_ThenRefused()
	{
		// Given
		var probe = Probe("""{ "streams": [ { "codec_type": "video" }, { "codec_type": "subtitle" } ] }""");

		// When / Then
		RemuxVerification.Reject(probe).ShouldNotBeNull();
	}

	[Fact]
	public void GivenStreamWithNoCodecType_WhenVerified_ThenRefused()
	{
		// Given — nothing says what it is, so nothing says it is safe
		var probe = Probe("""{ "streams": [ { "codec_type": "video" }, { } ] }""");

		// When
		var refusal = RemuxVerification.Reject(probe);

		// Then
		refusal.ShouldNotBeNull();
		refusal.ShouldContain("nameless");
	}

	[Fact]
	public void GivenLocationTagOnTheContainer_WhenVerified_ThenRefusedNamingTheTagNotItsValue()
	{
		// Given
		var probe = Probe("""
			{ "streams": [ { "codec_type": "video" } ],
			  "format": { "tags": { "major_brand": "isom", "location": "+49.2827-123.1207/" } } }
			""");

		// When
		var refusal = RemuxVerification.Reject(probe);

		// Then — the tag's name is safe to log; its value is the thing being kept in
		refusal.ShouldNotBeNull();
		refusal.ShouldContain("location");
		refusal.ShouldNotContain("49.2827");
	}

	[Fact]
	public void GivenDeviceTagOnAStream_WhenVerified_ThenRefused()
	{
		// Given
		var probe = Probe("""
			{ "streams": [ { "codec_type": "video", "tags": { "language": "und", "model": "iPhone 15 Pro" } } ] }
			""");

		// When
		var refusal = RemuxVerification.Reject(probe);

		// Then
		refusal.ShouldNotBeNull();
		refusal.ShouldContain("model");
	}

	[Fact]
	public void GivenATagNobodyHasHeardOf_WhenVerified_ThenRefused()
	{
		// Given — the reason this is an allowlist: a field a future phone invents
		// must fail closed rather than pass because nobody denylisted it
		var probe = Probe("""
			{ "streams": [ { "codec_type": "video" } ],
			  "format": { "tags": { "com.newphone.flight_path": "..." } } }
			""");

		// When / Then
		RemuxVerification.Reject(probe).ShouldNotBeNull();
	}

	[Fact]
	public void GivenNoStreamsOrTagsAtAll_WhenVerified_ThenAccepted()
	{
		// Given — nothing to object to
		RemuxVerification.Reject(Probe("{}")).ShouldBeNull();
	}

	[Fact]
	public void GivenTagsThatAreNotAnObject_WhenVerified_ThenAccepted()
	{
		// Given — ffprobe omits or empties `tags`; neither is a leak
		var probe = Probe("""{ "streams": [ { "codec_type": "video", "tags": null } ], "format": { } }""");

		// When / Then
		RemuxVerification.Reject(probe).ShouldBeNull();
	}
}
