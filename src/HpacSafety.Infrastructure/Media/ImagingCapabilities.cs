using HpacSafety.Core.Features.Reporting;
using ImageMagick;

namespace HpacSafety.Infrastructure.Media;

/// <summary>
/// What the imaging library in <i>this</i> runtime can actually do.
/// <para>
/// Magick.NET ships native binaries per platform and the delegates compiled into
/// them are not guaranteed to be the same everywhere. HEIC in particular needs
/// libheif, and a deployment that accepts HEIC without it would refuse every
/// iPhone upload as unrecognisable content — a silent degradation with no
/// diagnosable cause. So the codecs are checked, once, at startup, and a missing
/// one is a failure to start. See ADR-0025.
/// </para>
/// </summary>
public static class ImagingCapabilities
{
    /// <summary>True when the runtime can decode the format.</summary>
    public static bool CanDecode(MediaType type)
    {
        if (type.Kind is MediaKind.Video)
        {
            return false;
        }

        var format = MagickFormats.For(type);
        return MagickNET.SupportedFormats.Any(info => info.Format == format && info.SupportsReading);
    }

    /// <summary>
    /// Throws unless every strippable type in <paramref name="acceptedTypes" />
    /// can be decoded here. Call it while the process is starting.
    /// </summary>
    public static void EnsureCanDecode(IEnumerable<MediaType> acceptedTypes)
    {
        ArgumentNullException.ThrowIfNull(acceptedTypes);

        var missing = acceptedTypes
            .Where(type => type.CanBeStripped && !CanDecode(type))
            .Select(type => type.ContentType)
            .ToArray();

        if (missing.Length > 0)
        {
            throw new MissingImagingCodecException(
                $"This deployment accepts {string.Join(", ", missing)} but the imaging library in this runtime "
                + $"cannot decode {(missing.Length == 1 ? "it" : "them")}. "
                + "Refusing to start rather than rejecting every such upload as unrecognisable content. "
                + $"Imaging library: {MagickNET.Version}.");
        }
    }
}
