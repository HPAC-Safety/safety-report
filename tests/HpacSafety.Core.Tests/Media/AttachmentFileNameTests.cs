using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests.Media;

/// <summary>
///     A reporter's filename reaches a response header, so it is sanitized once, on
///     the way in, and the extension a reviewer's download carries always follows
///     the bytes served (ADR-0097, REQ-MED-019, REQ-MED-020).
/// </summary>
public class AttachmentFileNameTests
{
	private static readonly TinyId FileId = TinyId.Parse("kJQP7kiw5Fk");

	[Theory]
	[InlineData("launch-site.jpg", "launch-site.jpg")]
	[InlineData("reports/pilot/photo.jpg", "photo.jpg")]
	[InlineData("C:\\Users\\pilot\\photo.jpg", "photo.jpg")]
	[InlineData("../../etc/passwd.pdf", "passwd.pdf")]
	[InlineData("say \"cheese\";.png", "say cheese.png")]
	[InlineData("a<b>c:d*e?f.txt", "abcdef.txt")]
	[InlineData("  two   spaces .pdf  ", "two spaces .pdf")]
	[InlineData("tab\tbetween.pdf", "tabbetween.pdf")]
	[InlineData("line\r\nbreak.pdf", "linebreak.pdf")]
	[InlineData("..hidden.pdf", "hidden.pdf")]
	[InlineData("photo\u202Egpj.exe", "photogpj.exe")]
	[InlineData("Rapport d'accident é.pdf", "Rapport d'accident é.pdf")]
	public void GivenReporterFileName_WhenSanitized_ThenOnlySafeLastSegmentRemains(string given,
																				 string expected)
	{
		// Given / When
		var sanitized = AttachmentFileName.Sanitize(given);

		// Then
		sanitized.ShouldBe(expected);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("folder/")]
	[InlineData("\"\";;")]
	[InlineData("..")]
	public void GivenFileNameWithNothingUsable_WhenSanitized_ThenNone(string? given)
	{
		// Given / When / Then
		AttachmentFileName.Sanitize(given).ShouldBeNull();
	}

	[Fact]
	public void GivenOverlongFileName_WhenSanitized_ThenCutToLimit()
	{
		// Given
		var given = new string('a', 400) + ".pdf";

		// When
		var sanitized = AttachmentFileName.Sanitize(given);

		// Then
		sanitized!.Length.ShouldBe(AttachmentFileName.MaxLength);
	}

	[Fact]
	public void GivenOverlongNameEndingInEmojiAtTheLimit_WhenSanitized_ThenNoSurrogateIsLeftHalved()
	{
		// Given — 254 letters, then an emoji whose two UTF-16 halves straddle 255
		var given = new string('a', AttachmentFileName.MaxLength - 1) + "\U0001F681" + ".jpg";

		// When
		var sanitized = AttachmentFileName.Sanitize(given)!;

		// Then
		sanitized.Length.ShouldBe(AttachmentFileName.MaxLength - 1);
		char.IsHighSurrogate(sanitized[^1]).ShouldBeFalse();
	}

	[Fact]
	public void GivenHeicOriginalWithJpegDerivative_WhenNamedForDownload_ThenExtensionFollowsServedBytes()
	{
		// Given / When
		var name = AttachmentFileName.ForDownload("IMG_0412.HEIC", FileId, MediaType.Jpeg);

		// Then
		name.ShouldBe("IMG_0412.jpg");
	}

	[Fact]
	public void GivenNameClaimingAnotherType_WhenNamedForDownload_ThenServedExtensionReplacesIt()
	{
		// Given / When
		var name = AttachmentFileName.ForDownload("invoice.exe", FileId, MediaType.Pdf);

		// Then
		name.ShouldBe("invoice.pdf");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("   ")]
	[InlineData(";;")]
	public void GivenNoUsableName_WhenNamedForDownload_ThenFallsBackToFileId(string? original)
	{
		// Given / When
		var name = AttachmentFileName.ForDownload(original, FileId, MediaType.Pdf);

		// Then
		name.ShouldBe("kJQP7kiw5Fk.pdf");
	}

	[Fact]
	public void GivenLongStem_WhenNamedForDownload_ThenWholeNameStaysWithinLimit()
	{
		// Given / When
		var name = AttachmentFileName.ForDownload(new string('a', 300), FileId, MediaType.Jpeg);

		// Then
		name.Length.ShouldBe(AttachmentFileName.MaxLength);
		name.ShouldEndWith(".jpg");
	}
}
