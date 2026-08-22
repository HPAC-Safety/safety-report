using ImageMagick;

namespace HpacSafety.Infrastructure.Tests.Media;

/// <summary>
/// Builds the test media at run time rather than committing binaries. A fixture
/// generated here has no provenance to worry about and no real-looking data in
/// it — the coordinates below are the middle of the Pacific.
/// <para>
/// HEIC is the one exception, and it is a file rather than a generator because
/// the imaging library decodes HEIC but cannot encode it. See
/// <c>fixtures/README.md</c>.
/// </para>
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
    public static byte[] HeicWithGpsExif() =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Media", "fixtures", "gps.heic"));

    /// <summary>A PNG with no metadata at all.</summary>
    public static byte[] Png()
    {
        using var image = new MagickImage(MagickColors.Firebrick, 64, 64);
        image.Format = MagickFormat.Png;
        return image.ToByteArray();
    }

    /// <summary>
    /// The opening boxes of an MP4. Only the container is real: nothing in this
    /// system decodes a video, so twelve bytes of <c>ftyp</c> is the whole of
    /// what is under test.
    /// </summary>
    public static byte[] Mp4() => IsoBaseMediaContainer("isom");

    /// <summary>The opening boxes of a QuickTime file, an iPhone's video default.</summary>
    public static byte[] QuickTime() => IsoBaseMediaContainer("qt  ");

    /// <summary>Bytes that are not media in any format this system accepts.</summary>
    public static byte[] NotMedia() =>
        "%PDF-1.7\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n"u8.ToArray();

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
        System.Text.Encoding.ASCII.GetBytes(brand).CopyTo(bytes.AsSpan(8));

        return bytes;
    }
}
