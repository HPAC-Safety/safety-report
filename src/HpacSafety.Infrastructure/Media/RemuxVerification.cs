using System.Text.Json;

namespace HpacSafety.Infrastructure.Media;

/// <summary>
///     Decides whether a remuxed file is safe to show a reviewer, from what ffprobe
///     says about it. A pure reading of the probe: no process, no file, so the
///     refusals can be exercised directly rather than by finding a video that
///     provokes each one. See ADR-0094.
/// </summary>
public static class RemuxVerification
{
	/// <summary>
	///     An allowlist, not a denylist. A denylist of known-bad names — location,
	///     make, model — passes anything it has not heard of, and the field that
	///     matters is the one somebody's next phone invents. These are the tags a
	///     remux legitimately writes: container structure, and the language and
	///     handler name a track carries.
	/// </summary>
	public static readonly string[] StructuralTags =
	[
		"major_brand", "minor_version", "compatible_brands", "language", "handler_name", "vendor_id",
	];

	/// <summary>
	///     Why this file is not a derivative, or <see langword="null" /> when it is
	///     one. The reason names the offending stream kind or tag <i>name</i> — never
	///     a tag's value, which is the thing being kept out of the open.
	/// </summary>
	public static string? Reject(JsonElement probe)
	{
		if (probe.TryGetProperty("streams", out var streams))
		{
			foreach (var stream in streams.EnumerateArray())
			{
				var kind = stream.TryGetProperty("codec_type", out var codecType) ? codecType.GetString() : null;

				if (kind is not ("video" or "audio"))
				{
					return $"Remux left a {kind ?? "nameless"} stream in place";
				}

				if (UnexpectedTag(stream) is { } inStream)
				{
					return $"Remux left the stream tag {inStream} in place";
				}
			}
		}

		return probe.TryGetProperty("format", out var format) && UnexpectedTag(format) is { } inFormat
			? $"Remux left the tag {inFormat} in place"
			: null;
	}

	private static string? UnexpectedTag(JsonElement element)
	{
		if (!element.TryGetProperty("tags", out var tags) || tags.ValueKind is not JsonValueKind.Object)
		{
			return null;
		}

		// Not FirstOrDefault: a default JsonProperty throws when its Name is read,
		// so "nothing unexpected" would crash on exactly the files that are clean.
		foreach (var tag in tags.EnumerateObject())
		{
			if (!StructuralTags.Contains(tag.Name, StringComparer.OrdinalIgnoreCase))
			{
				return tag.Name;
			}
		}

		return null;
	}
}
