using ImageMagick;

namespace HpacSafety.Infrastructure.Tests.Media;

/// <summary>
/// Builds the test images at run time rather than committing binaries. A fixture
/// generated here has no provenance to worry about and no real-looking data in
/// it — the coordinates below are the middle of the Pacific.
/// </summary>
internal static class ExifFixtures
{
    /// <summary>The camera make written into the fixture, asserted absent from the derivative.</summary>
    public const string CameraMake = "HpacFixtureCamera";

    /// <summary>A JPEG carrying GPS coordinates, a camera make, and a capture timestamp.</summary>
    public static byte[] JpegWithGpsExif()
    {
        using var image = new MagickImage(MagickColors.SkyBlue, 64, 64);

        var exif = new ExifProfile();
        exif.SetValue(ExifTag.GPSLatitudeRef, "N");
        exif.SetValue(ExifTag.GPSLatitude, [new Rational(23), new Rational(45), new Rational(6)]);
        exif.SetValue(ExifTag.GPSLongitudeRef, "W");
        exif.SetValue(ExifTag.GPSLongitude, [new Rational(150), new Rational(12), new Rational(9)]);
        exif.SetValue(ExifTag.Make, CameraMake);
        exif.SetValue(ExifTag.DateTimeOriginal, "2026:08:22 12:34:56");

        image.SetProfile(exif);
        image.Format = MagickFormat.Jpeg;

        return image.ToByteArray();
    }

    /// <summary>A PNG with no metadata at all.</summary>
    public static byte[] Png()
    {
        using var image = new MagickImage(MagickColors.Firebrick, 64, 64);
        image.Format = MagickFormat.Png;
        return image.ToByteArray();
    }

    /// <summary>Bytes that are not an image in any format this system accepts.</summary>
    public static byte[] NotAnImage() =>
        "%PDF-1.7\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n"u8.ToArray();
}
