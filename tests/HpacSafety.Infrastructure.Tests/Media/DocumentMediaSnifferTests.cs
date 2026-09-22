using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Media;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Media;

public class DocumentMediaSnifferTests
{
	private readonly DocumentMediaSniffer _sniffer = new();

	[Fact]
	public async Task GivenPdf_WhenSniffed_ThenReportedAsPdf()
	{
		// Given
		using var content = new MemoryStream(ExifFixtures.Pdf());

		// When
		var sniffed = await _sniffer.Sniff(content, CancellationToken.None);

		// Then
		sniffed.ShouldBe(MediaType.Pdf);
	}

	[Fact]
	public async Task GivenOle2Header_WhenSniffed_ThenReportedAsDoc()
	{
		// Given
		using var content = new MemoryStream(ExifFixtures.Doc());

		// When
		var sniffed = await _sniffer.Sniff(content, CancellationToken.None);

		// Then
		sniffed.ShouldBe(MediaType.Doc);
	}

	[Fact]
	public async Task GivenRtf_WhenSniffed_ThenReportedAsCanonicalRtf()
	{
		// Given
		using var content = new MemoryStream(ExifFixtures.Rtf());

		// When
		var sniffed = await _sniffer.Sniff(content, CancellationToken.None);

		// Then
		// A sniffer only ever sees bytes — the text/rtf alias only exists at
		// the declared-content-type layer, in MediaType.
		sniffed.ShouldBe(MediaType.Rtf);
	}

	[Fact]
	public async Task GivenDocxPackage_WhenSniffed_ThenReportedAsDocx()
	{
		// Given
		using var content = new MemoryStream(ExifFixtures.Docx());

		// When
		var sniffed = await _sniffer.Sniff(content, CancellationToken.None);

		// Then
		sniffed.ShouldBe(MediaType.Docx);
	}

	[Fact]
	public async Task GivenOdtPackage_WhenSniffed_ThenReportedAsOdt()
	{
		// Given
		using var content = new MemoryStream(ExifFixtures.Odt());

		// When
		var sniffed = await _sniffer.Sniff(content, CancellationToken.None);

		// Then
		sniffed.ShouldBe(MediaType.Odt);
	}

	[Fact]
	public async Task GivenPlainZipThatIsNeitherPackage_WhenSniffed_ThenUnrecognised()
	{
		// Given
		using var content = new MemoryStream(ExifFixtures.PlainZip());

		// When
		var sniffed = await _sniffer.Sniff(content, CancellationToken.None);

		// Then
		sniffed.ShouldBeNull();
	}

	[Fact]
	public async Task GivenMalformedZip_WhenSniffed_ThenUnrecognised()
	{
		// Given
		using var content = new MemoryStream(ExifFixtures.MalformedZip());

		// When
		var sniffed = await _sniffer.Sniff(content, CancellationToken.None);

		// Then
		sniffed.ShouldBeNull();
	}

	[Fact]
	public async Task GivenPlausibleText_WhenSniffed_ThenReportedAsPlainText()
	{
		// Given
		using var content = new MemoryStream(ExifFixtures.PlainText());

		// When
		var sniffed = await _sniffer.Sniff(content, CancellationToken.None);

		// Then
		// Markdown and plain text are the same byte-level answer — this system
		// never parses or renders either one.
		sniffed.ShouldBe(MediaType.PlainText);
	}

	[Fact]
	public async Task GivenBinaryGarbage_WhenSniffed_ThenNotClaimedAsText()
	{
		// Given
		using var content = new MemoryStream(ExifFixtures.BinaryGarbage());

		// When
		var sniffed = await _sniffer.Sniff(content, CancellationToken.None);

		// Then
		sniffed.ShouldBeNull();
	}

	[Fact]
	public async Task GivenTextWithADisallowedControlCharacter_WhenSniffed_ThenNotClaimedAsText()
	{
		// Given
		using var content = new MemoryStream(ExifFixtures.TextWithDisallowedControlCharacter());

		// When
		var sniffed = await _sniffer.Sniff(content, CancellationToken.None);

		// Then
		sniffed.ShouldBeNull();
	}

	[Fact]
	public async Task GivenEmptyContent_WhenSniffed_ThenUnrecognised()
	{
		// Given
		using var content = new MemoryStream([]);

		// When
		var sniffed = await _sniffer.Sniff(content, CancellationToken.None);

		// Then
		sniffed.ShouldBeNull();
	}

	[Fact]
	public async Task GivenJpeg_WhenSniffedByDocumentSniffer_ThenNotClaimedAsDocument()
	{
		// Given
		using var content = new MemoryStream(ExifFixtures.JpegWithGpsExif());

		// When
		var sniffed = await _sniffer.Sniff(content, CancellationToken.None);

		// Then
		sniffed.ShouldBeNull();
	}
}
