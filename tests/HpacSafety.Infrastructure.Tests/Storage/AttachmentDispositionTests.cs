using HpacSafety.Infrastructure.Storage;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Storage;

/// <summary>
///     A reporter's filename rides a response header, so it is always a forced
///     download, and a French name survives through <c>filename*</c> (ADR-0097).
/// </summary>
public class AttachmentDispositionTests
{
	[Fact]
	public void GivenAsciiName_WhenHeaderIsBuilt_ThenBothFormsCarryIt()
	{
		// Given / When
		var header = AttachmentDisposition.For("launch-site.jpg");

		// Then
		header.ShouldBe("attachment; filename=\"launch-site.jpg\"; filename*=UTF-8''launch-site.jpg");
	}

	[Fact]
	public void GivenAccentedName_WhenHeaderIsBuilt_ThenFallbackIsAsciiAndStarFormIsPercentEncoded()
	{
		// Given / When
		var header = AttachmentDisposition.For("Rapport d'été.pdf");

		// Then
		header.ShouldBe("attachment; filename=\"Rapport d'_t_.pdf\"; filename*=UTF-8''Rapport%20d%27%C3%A9t%C3%A9.pdf");
	}

	[Fact]
	public void GivenQuoteOrBackslash_WhenHeaderIsBuilt_ThenFallbackCannotBreakOutOfItsQuotes()
	{
		// Given / When
		var header = AttachmentDisposition.For("a\"b\\c.pdf");

		// Then
		header.ShouldStartWith("attachment; filename=\"a_b_c.pdf\";");
	}
}
