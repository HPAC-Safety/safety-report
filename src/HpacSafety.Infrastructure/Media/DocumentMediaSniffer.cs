using System.IO.Compression;
using System.Text;
using HpacSafety.Core.Features.Reporting;

namespace HpacSafety.Infrastructure.Media;

/// <summary>
///     Recognises the document formats this system accepts, by magic number and, for
///     the two zip-based formats, the internal package shape a magic number alone
///     cannot tell apart.
///     <para>
///         No library beyond the base class library's own <see cref="ZipArchive" /> is
///         involved — nothing here parses a document's actual content, only enough of
///         its container to know what kind of container it is. A document is never
///         extracted or transformed; see ADR-0089 (no malware scan either — format
///         validation is the only gate) and issue #310.
///     </para>
/// </summary>
public sealed class DocumentMediaSniffer : IMediaSniffer
{
	/// <summary>
	///     How far a plain-text guess ever looks, however large the upload — a
	///     pathological file cannot make this scan cost more than a fixed, small
	///     amount of work.
	/// </summary>
	private const int BoundedTextScanLength = 8192;

	private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

	private static ReadOnlySpan<byte> PdfSignature => "%PDF-"u8;
	private static ReadOnlySpan<byte> OleSignature => [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
	private static ReadOnlySpan<byte> ZipSignature => [0x50, 0x4B, 0x03, 0x04];
	private static ReadOnlySpan<byte> RtfSignature => "{\\rtf1"u8;

	/// <inheritdoc />
	public async Task<MediaType?> Sniff(Stream content, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(content);

		using var buffered = new MemoryStream();
		await content.CopyToAsync(buffered, cancellationToken).ConfigureAwait(false);
		var bytes = buffered.ToArray();

		if (StartsWith(bytes, PdfSignature))
		{
			return MediaType.Pdf;
		}

		if (StartsWith(bytes, OleSignature))
		{
			return MediaType.Doc;
		}

		if (StartsWith(bytes, RtfSignature))
		{
			return MediaType.Rtf;
		}

		if (StartsWith(bytes, ZipSignature))
		{
			return SniffZipPackage(buffered);
		}

		return SniffPlainText(bytes);
	}

	private static bool StartsWith(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> signature)
	{
		return bytes.Length >= signature.Length && bytes[..signature.Length].SequenceEqual(signature);
	}

	/// <summary>
	///     A zip's magic number alone does not say what it is — DOCX, ODT, and an
	///     ordinary zip archive all share it. The internal shape decides: ODT's first
	///     entry is an uncompressed <c>mimetype</c> file naming itself, per the
	///     OpenDocument specification; DOCX is an OOXML package, which always carries
	///     a root <c>[Content_Types].xml</c> entry.
	/// </summary>
	private static MediaType? SniffZipPackage(MemoryStream buffered)
	{
		buffered.Position = 0;

		try
		{
			using var archive = new ZipArchive(buffered, ZipArchiveMode.Read, leaveOpen: true);

			if (IsOpenDocumentText(archive))
			{
				return MediaType.Odt;
			}

			return archive.GetEntry("[Content_Types].xml") is not null ? MediaType.Docx : null;
		}
		catch (InvalidDataException)
		{
			// Not a failure — the bytes only claimed to be a zip. See IMediaSniffer.
			return null;
		}
	}

	private static bool IsOpenDocumentText(ZipArchive archive)
	{
		if (archive.Entries is not [{ FullName: "mimetype" } first, ..])
		{
			return false;
		}

		using var stream = first.Open();
		using var reader = new StreamReader(stream, Encoding.ASCII);
		return reader.ReadToEnd().Trim() == MediaType.Odt.ContentType;
	}

	/// <summary>
	///     Markdown and plain text share one byte-level answer: this system never
	///     renders or parses either one, so there is nothing content can tell them
	///     apart by. A bounded prefix must decode as UTF-8 with no embedded NUL and no
	///     control character beyond tab/newline/carriage-return, or the bytes are not
	///     recognised as text at all.
	/// </summary>
	private static MediaType? SniffPlainText(byte[] bytes)
	{
		if (bytes.Length == 0)
		{
			return null;
		}

		var scanned = bytes.AsSpan(0, Math.Min(bytes.Length, BoundedTextScanLength));

		return IsPlausibleText(scanned) ? MediaType.PlainText : null;
	}

	private static bool IsPlausibleText(ReadOnlySpan<byte> bytes)
	{
		if (bytes.IndexOf((byte)0) >= 0)
		{
			return false;
		}

		var chars = new char[bytes.Length];

		try
		{
			// flush: false tolerates a multi-byte sequence truncated at the scan
			// boundary without treating it as invalid — only a genuinely malformed
			// sequence throws.
			var decoder = StrictUtf8.GetDecoder();
			decoder.Convert(bytes, chars, flush: false, out _, out var charsUsed, out _);

			foreach (var character in chars.AsSpan(0, charsUsed))
			{
				if (char.IsControl(character) && character is not ('\t' or '\n' or '\r'))
				{
					return false;
				}
			}
		}
		catch (DecoderFallbackException)
		{
			return false;
		}

		return true;
	}
}
