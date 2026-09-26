namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     The largest file this deployment accepts, one limit per kind: a phone video
///     of a flight is routinely hundreds of megabytes, while an image or a document
///     rarely needs more than a few (ADR-0126).
/// </summary>
public sealed record MediaSizeLimits
{
	/// <summary>Creates the limits. Each is explicit; there is no default here to inherit by accident.</summary>
	/// <param name="image">The largest image, in bytes.</param>
	/// <param name="video">The largest video, in bytes.</param>
	/// <param name="document">The largest document, in bytes.</param>
	public MediaSizeLimits(long image,
						   long video,
						   long document)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(image);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(video);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(document);

		Image = image;
		Video = video;
		Document = document;
	}

	/// <summary>The largest image, in bytes.</summary>
	public long Image { get; }

	/// <summary>The largest video, in bytes.</summary>
	public long Video { get; }

	/// <summary>The largest document, in bytes.</summary>
	public long Document { get; }

	/// <summary>
	///     The largest file of any kind: the most a reader ever needs to take before
	///     it knows what the file is.
	/// </summary>
	public long Largest => Math.Max(Image, Math.Max(Video, Document));

	/// <summary>One limit for every kind.</summary>
	public static MediaSizeLimits Uniform(long byteSize)
	{
		return new MediaSizeLimits(byteSize, byteSize, byteSize);
	}

	/// <summary>The limit for one kind.</summary>
	public long For(MediaKind kind)
	{
		return kind switch
		{
			MediaKind.Image => Image,
			MediaKind.Video => Video,
			MediaKind.Document => Document,
			_ => throw new ArgumentOutOfRangeException(nameof(kind)),
		};
	}
}
