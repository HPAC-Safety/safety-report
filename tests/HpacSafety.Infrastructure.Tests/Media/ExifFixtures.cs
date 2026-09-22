using System.IO.Compression;
using System.Text;
using HpacSafety.Core.Features.Reporting;
using ImageMagick;

namespace HpacSafety.Infrastructure.Tests.Media;

/// <summary>
///     Builds the test media at run time rather than committing binaries. A fixture
///     generated here has no provenance to worry about and no real-looking data in
///     it — the coordinates below are the middle of the Pacific.
///     <para>
///         HEIC is the one exception, and it is a file rather than a generator because
///         the imaging library decodes HEIC but cannot encode it. See
///         <c>fixtures/README.md</c>.
///     </para>
/// </summary>
internal static class ExifFixtures
{
	/// <summary>The camera make written into the fixtures, asserted absent from a derivative.</summary>
	public const string CameraMake = "HpacFixtureCamera";

	/// <summary>The capture timestamp written into the fixtures, asserted absent from a derivative.</summary>
	public const string CapturedAt = "2026:08:22 12:34:56";

	/// <summary>A JPEG carrying GPS coordinates, a camera make, and a capture timestamp.</summary>
	public static byte[] JpegWithGpsExif()
	{
		using var image = new MagickImage(MagickColors.SkyBlue, 64, 64);
		image.SetProfile(GpsProfile());
		image.Format = MagickFormat.Jpeg;

		return image.ToByteArray();
	}

	/// <summary>The committed HEIC fixture, carrying the same synthetic EXIF as the JPEG.</summary>
	public static byte[] HeicWithGpsExif()
	{
		return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Media", "fixtures", "gps.heic"));
	}

	/// <summary>A PNG with no metadata at all.</summary>
	public static byte[] Png()
	{
		using var image = new MagickImage(MagickColors.Firebrick, 64, 64);
		image.Format = MagickFormat.Png;
		return image.ToByteArray();
	}

	/// <summary>
	///     The opening boxes of an MP4. Only the container is real: nothing in this
	///     system decodes a video, so twelve bytes of <c>ftyp</c> is the whole of
	///     what is under test.
	/// </summary>
	public static byte[] Mp4()
	{
		return IsoBaseMediaContainer("isom");
	}

	/// <summary>The opening boxes of a QuickTime file, an iPhone's video default.</summary>
	public static byte[] QuickTime()
	{
		return IsoBaseMediaContainer("qt  ");
	}

	/// <summary>
	///     Bytes that are not an image or a video — a PDF is a document this system
	///     now accepts (issue #310), which is exactly why this fixture is useful
	///     where it is used: to prove an image/video-specific sniffer does not claim
	///     something that plainly is not one.
	/// </summary>
	public static byte[] NotMedia()
	{
		return "%PDF-1.7\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n"u8.ToArray();
	}

	/// <summary>
	///     Bytes no sniffer this system runs recognises at all — not an image, video,
	///     or document magic number, and not plausible text either (an embedded NUL
	///     byte rules that out).
	/// </summary>
	public static byte[] UnrecognisedByAnySniffer()
	{
		return [0x00, 0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0xFF, 0x00, 0x13, 0x37, 0x00, 0x00];
	}

	/// <summary>A minimal, syntactically valid PDF.</summary>
	public static byte[] Pdf()
	{
		return "%PDF-1.7\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n"u8.ToArray();
	}

	/// <summary>The OLE2 compound-file header a legacy <c>.doc</c> starts with.</summary>
	public static byte[] Doc()
	{
		var bytes = new byte[16];
		byte[] header = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
		header.CopyTo(bytes.AsSpan());
		return bytes;
	}

	/// <summary>The plainest possible RTF document.</summary>
	public static byte[] Rtf()
	{
		return "{\\rtf1\\ansi Hello}"u8.ToArray();
	}

	/// <summary>A minimal OOXML package — the shape any DOCX/XLSX/PPTX shares.</summary>
	public static byte[] Docx()
	{
		using var buffered = new MemoryStream();
		using (var archive = new ZipArchive(buffered, ZipArchiveMode.Create, leaveOpen: true))
		{
			var entry = archive.CreateEntry("[Content_Types].xml");
			using var writer = new StreamWriter(entry.Open());
			writer.Write("<?xml version=\"1.0\"?><Types/>");
		}

		return buffered.ToArray();
	}

	/// <summary>A minimal OpenDocument Text package — first entry an uncompressed <c>mimetype</c>.</summary>
	public static byte[] Odt()
	{
		using var buffered = new MemoryStream();
		using (var archive = new ZipArchive(buffered, ZipArchiveMode.Create, leaveOpen: true))
		{
			var entry = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
			using var writer = new StreamWriter(entry.Open());
			writer.Write(MediaType.Odt.ContentType);
		}

		return buffered.ToArray();
	}

	/// <summary>A well-formed zip that is neither DOCX nor ODT — an ordinary archive.</summary>
	public static byte[] PlainZip()
	{
		using var buffered = new MemoryStream();
		using (var archive = new ZipArchive(buffered, ZipArchiveMode.Create, leaveOpen: true))
		{
			var entry = archive.CreateEntry("readme.txt");
			using var writer = new StreamWriter(entry.Open());
			writer.Write("not a document package");
		}

		return buffered.ToArray();
	}

	/// <summary>Bytes that claim the zip magic number but are not a valid archive.</summary>
	public static byte[] MalformedZip()
	{
		return [0x50, 0x4B, 0x03, 0x04, 0x00, 0x00, 0x00, 0x00];
	}

	/// <summary>Plausible plain-text bytes — ordinary prose, the shape Markdown and TXT share.</summary>
	public static byte[] PlainText()
	{
		return "Line one\nLine two\r\nTab\there.\n"u8.ToArray();
	}

	/// <summary>Binary garbage that must never be mistaken for text — an embedded NUL byte.</summary>
	public static byte[] BinaryGarbage()
	{
		return [0x41, 0x42, 0x00, 0x43, 0x44, 0x01, 0x02, 0x03];
	}

	/// <summary>
	///     Valid UTF-8 with no embedded NUL, disqualified only by a control character
	///     other than tab/newline/carriage-return — the one text-rejection path a NUL
	///     byte can never reach on its own.
	/// </summary>
	public static byte[] TextWithDisallowedControlCharacter()
	{
		return "Line one\x07Line two\n"u8.ToArray();
	}

	private static ExifProfile GpsProfile()
	{
		var exif = new ExifProfile();
		exif.SetValue(ExifTag.GPSLatitudeRef, "N");
		exif.SetValue(ExifTag.GPSLatitude, [new Rational(23), new Rational(45), new Rational(6)]);
		exif.SetValue(ExifTag.GPSLongitudeRef, "W");
		exif.SetValue(ExifTag.GPSLongitude, [new Rational(150), new Rational(12), new Rational(9)]);
		exif.SetValue(ExifTag.Make, CameraMake);
		exif.SetValue(ExifTag.DateTimeOriginal, CapturedAt);

		return exif;
	}

	private static byte[] IsoBaseMediaContainer(string brand)
	{
		// size(4) + "ftyp"(4) + major brand(4) + minor version(4), then padding
		// so the buffer is long enough for a sniffer's header read.
		var bytes = new byte[64];
		bytes[3] = 0x18;
		"ftyp"u8.CopyTo(bytes.AsSpan(4));
		Encoding.ASCII.GetBytes(brand).CopyTo(bytes.AsSpan(8));

		return bytes;
	}
}
