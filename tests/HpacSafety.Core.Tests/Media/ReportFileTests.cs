using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests.Media;

/// <summary>
///     <see cref="MediaIngestOutcome" /> is transient; <see cref="ReportFile" /> is
///     the row an admin UI will actually project from. The fail-closed rule has to
///     hold on both, or it holds only until the first page is written.
/// </summary>
public class ReportFileTests
{
	private const string ReportId = "dQw4w9WgXcQ";

	private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

	private static ReportFile NewFile(string fileName = "photo.jpg")
	{
		return new ReportFile(TinyId.New(), BlobKey.For(ReportId, MediaCompartment.Original, fileName).Value, "image/jpeg", 1024, Now);
	}

	[Theory]
	[InlineData("image/jpeg", AttachmentKind.Image)]
	[InlineData("video/mp4", AttachmentKind.Video)]
	[InlineData("application/pdf", AttachmentKind.Document)]
	[InlineData("application/octet-stream", AttachmentKind.Document)]
	public void GivenAContentType_WhenRecorded_ThenKindReflectsIt(string contentType, AttachmentKind expected)
	{
		// Given / When
		var file = new ReportFile(TinyId.New(), BlobKey.For(ReportId, MediaCompartment.Original, "file.bin").Value, contentType, 1024, Now);

		// Then
		// An unparseable content type falls back to Document, the same as any
		// other format this system never strips — see #79.
		file.Kind.ShouldBe(expected);
	}

	[Fact]
	public void GivenFileWithNoDerivative_WhenViewableKeyIsAskedFor_ThenFailsClosed()
	{
		// Given
		var file = NewFile();

		// When / Then
		// Returning BlobKey — the unstripped original — is the leak this whole
		// feature exists to prevent.
		file.AwaitsStripping.ShouldBeTrue();
		Should.Throw<DomainRuleViolationException>(() => file.ViewableKey);
	}

	[Fact]
	public void GivenStrippedFile_WhenViewableKeyIsAskedFor_ThenDerivative()
	{
		// Given
		var file = NewFile();
		var derivative = BlobKey.For(ReportId, MediaCompartment.Stripped, "photo.jpg");

		// When
		file.RecordStripped(derivative.Value, Now);

		// Then
		file.AwaitsStripping.ShouldBeFalse();
		file.ViewableKey.ShouldBe(derivative);
		file.ViewableKey.Compartment.ShouldBe(MediaCompartment.Stripped);
	}

	[Fact]
	public void GivenVideo_WhenRecorded_ThenStaysUnviewable()
	{
		// Given
		// A video is retained with no derivative until #65. It must read as
		// awaiting stripping for as long as that is true.
		var file = NewFile("clip.mp4");

		// When / Then
		file.AwaitsStripping.ShouldBeTrue();
		Should.Throw<DomainRuleViolationException>(() => file.ViewableKey);
	}

	[Fact]
	public void GivenKeyOutsideStrippedCompartment_WhenRecordedAsDerivative_ThenRefused()
	{
		// Given
		var file = NewFile();
		var original = BlobKey.For(ReportId, MediaCompartment.Original, "photo.jpg");

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => file.RecordStripped(original.Value, Now));
		file.AwaitsStripping.ShouldBeTrue();
	}

	[Fact]
	public void GivenDerivativeIsRecorded_WhenRowIsRead_ThenBothFactsWereWrittenTogether()
	{
		// Given
		var file = NewFile();

		// When
		file.RecordStripped(BlobKey.For(ReportId, MediaCompartment.Stripped, "photo.jpg").Value, Now);

		// Then
		// AwaitsStripping checks both, so a row carrying a timestamp with no key
		// cannot read as viewable.
		file.ExifStrippedAt.ShouldBe(Now);
		file.StrippedBlobKey.ShouldNotBeNull();
	}
}
