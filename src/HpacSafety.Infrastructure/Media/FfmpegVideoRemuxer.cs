using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using HpacSafety.Core.Features.Reporting;
using Microsoft.Extensions.Logging;

namespace HpacSafety.Infrastructure.Media;

/// <summary>
///     An <b>Adapter</b> over ffmpeg that produces a video derivative by remuxing:
///     the compressed packets move into a fresh container, and every tag and every
///     non-audiovisual track is left behind. See ADR-0094.
///     <para>
///         It never transcodes. A remux does not decode the picture, so the bytes a
///         reporter uploaded are parsed by a demuxer rather than by an H.264 or HEVC
///         decoder — the smaller and less exploited of the two. It is also lossless,
///         which matters for footage somebody will study.
///     </para>
///     <para>
///         ffmpeg runs as a child process with a fixed argument list and no shell, so
///         nothing from the upload reaches a command line. Input and output are
///         temporary files the caller never names: ffmpeg needs to seek, and a phone
///         recording's `moov` atom is routinely at the end of the file.
///     </para>
/// </summary>
public sealed partial class FfmpegVideoRemuxer : IVideoRemuxer
{
	// Selected, not filtered. -map 0:v:0 -map 0:a? takes the first video track and
	// audio if there is any; the negative maps drop data, subtitle and timed
	// metadata. That last one is the point: an iPhone recording carries `mebx`
	// tracks holding motion and sometimes location, and a tag-based wipe leaves
	// them untouched while the file looks clean.
	private static readonly string[] RemuxArguments =
	[
		"-nostdin", "-hide_banner", "-loglevel", "error", "-y",
		"-i", "{input}",
		"-map", "0:v:0", "-map", "0:a?", "-map", "-0:d", "-map", "-0:s", "-map", "-0:t",
		"-c", "copy",
		"-map_metadata", "-1", "-map_metadata:s:v", "-1", "-map_metadata:s:a", "-1",
		// Without this ffmpeg stamps its own build into an `encoder` tag, which
		// is harmless but is still a tag we did not put there — and the
		// verification below allows only what it recognises.
		"-bitexact",
		"-movflags", "+faststart",
		"{output}",
	];

	// An allowlist, not a denylist. A denylist of known-bad names — location,
	// make, model — passes anything it has not heard of, and the whole point of
	// this check is the field somebody's next phone invents. These are the tags
	// a remux legitimately writes: container structure, and the language and
	// handler name a track carries.
	private static readonly string[] StructuralTags =
	[
		"major_brand", "minor_version", "compatible_brands", "language", "handler_name", "vendor_id",
	];

	private readonly ILogger<FfmpegVideoRemuxer> _logger;
	private readonly TimeSpan _timeout;

	public FfmpegVideoRemuxer(ILogger<FfmpegVideoRemuxer> logger, TimeSpan? timeout = null)
	{
		ArgumentNullException.ThrowIfNull(logger);

		_logger = logger;
		_timeout = timeout ?? TimeSpan.FromMinutes(2);
	}

	/// <inheritdoc />
	public async Task<bool> TryRemux(
		Stream source, Stream destination, MediaType type, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(source);
		ArgumentNullException.ThrowIfNull(destination);

		var workspace = Directory.CreateTempSubdirectory("hpac-remux-");

		try
		{
			var input = Path.Combine(workspace.FullName, "input");
			var output = Path.Combine(workspace.FullName, "output.mp4");

			await using (var inputFile = File.Create(input))
			{
				source.Position = 0;
				await source.CopyToAsync(inputFile, cancellationToken).ConfigureAwait(false);
			}

			if (!await Run("ffmpeg", Arguments(input, output), cancellationToken).ConfigureAwait(false))
			{
				return false;
			}

			if (!File.Exists(output) || new FileInfo(output).Length == 0)
			{
				LogNoOutput(_logger);
				return false;
			}

			if (!await IsClean(output, cancellationToken).ConfigureAwait(false))
			{
				return false;
			}

			await using var produced = File.OpenRead(output);
			await produced.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
			return true;
		}
		finally
		{
			// A partially written destination must never be mistaken for a
			// derivative, so the caller's guarantee is enforced here too.
			TryDelete(workspace);
		}
	}

	private static IEnumerable<string> Arguments(string input, string output)
	{
		return RemuxArguments.Select(argument => argument
			.Replace("{input}", input, StringComparison.Ordinal)
			.Replace("{output}", output, StringComparison.Ordinal));
	}

	/// <summary>
	///     Whether the produced file holds only the streams it should and no tag
	///     naming a person, device, or place. A derivative that fails this is not a
	///     derivative — the original is retained instead.
	/// </summary>
	private async Task<bool> IsClean(string path, CancellationToken cancellationToken)
	{
		var probe = await Capture(
			"ffprobe",
			["-hide_banner", "-loglevel", "error", "-print_format", "json", "-show_format", "-show_streams", path],
			cancellationToken).ConfigureAwait(false);

		if (probe is null)
		{
			return false;
		}

		using var document = JsonDocument.Parse(probe);
		var root = document.RootElement;

		if (root.TryGetProperty("streams", out var streams))
		{
			foreach (var stream in streams.EnumerateArray())
			{
				var codecType = stream.TryGetProperty("codec_type", out var kind) ? kind.GetString() : null;

				if (codecType is not ("video" or "audio"))
				{
					LogExtraStream(_logger, codecType);
					return false;
				}

				if (HoldsForbiddenTag(stream))
				{
					return false;
				}
			}
		}

		return !root.TryGetProperty("format", out var format) || !HoldsForbiddenTag(format);
	}

	private bool HoldsForbiddenTag(JsonElement element)
	{
		if (!element.TryGetProperty("tags", out var tags) || tags.ValueKind is not JsonValueKind.Object)
		{
			return false;
		}

		foreach (var tag in tags.EnumerateObject())
		{
			if (StructuralTags.Contains(tag.Name, StringComparer.OrdinalIgnoreCase))
			{
				continue;
			}

			// The tag's name, never its value: the value is the thing we are
			// trying not to disclose.
			LogRemainingTag(_logger, tag.Name);
			return true;
		}

		return false;
	}

	private async Task<bool> Run(string executable, IEnumerable<string> arguments, CancellationToken cancellationToken)
	{
		return await Capture(executable, arguments, cancellationToken).ConfigureAwait(false) is not null;
	}

	/// <summary>
	///     Runs the tool and returns its standard output, or null when it is absent,
	///     fails, or outruns the timeout. An absent toolchain is a retained original,
	///     not an exception: a deployment without ffmpeg still accepts video.
	/// </summary>
	private async Task<string?> Capture(
		string executable, IEnumerable<string> arguments, CancellationToken cancellationToken)
	{
		var start = new ProcessStartInfo(executable)
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			// No shell, so nothing from the upload is ever parsed as a command.
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		foreach (var argument in arguments)
		{
			start.ArgumentList.Add(argument);
		}

		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(_timeout);

		try
		{
			using var process = Process.Start(start);

			if (process is null)
			{
				return null;
			}

			var standardOutput = process.StandardOutput.ReadToEndAsync(timeout.Token);
			var standardError = process.StandardError.ReadToEndAsync(timeout.Token);

			await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);

			if (process.ExitCode == 0)
			{
				return await standardOutput.ConfigureAwait(false);
			}

			// The tool's own diagnostics, which describe the container rather
			// than its contents.
			LogToolFailed(
				_logger,
				executable,
				process.ExitCode.ToString(CultureInfo.InvariantCulture),
				(await standardError.ConfigureAwait(false)).Trim());

			return null;
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			LogTimedOut(_logger, executable, _timeout);
			return null;
		}
		catch (Exception cause) when (cause is System.ComponentModel.Win32Exception or FileNotFoundException)
		{
			// Not installed. The original is retained and the upload still
			// succeeds, which is the whole point of REQ-MED-015.
			LogToolMissing(_logger, executable);
			return null;
		}
	}

	private void TryDelete(DirectoryInfo workspace)
	{
		try
		{
			workspace.Delete(true);
		}
		catch (IOException cause)
		{
			LogWorkspaceNotCleared(_logger, cause.Message);
		}
	}

	[LoggerMessage(Level = LogLevel.Warning, Message = "Remux produced no output; the original is retained instead.")]
	private static partial void LogNoOutput(ILogger logger);

	[LoggerMessage(
		Level = LogLevel.Warning,
		Message = "Remux left a {CodecType} stream in place; the original is retained instead.")]
	private static partial void LogExtraStream(ILogger logger, string? codecType);

	[LoggerMessage(
		Level = LogLevel.Warning,
		Message = "Remux left the tag {Tag} in place; the original is retained instead.")]
	private static partial void LogRemainingTag(ILogger logger, string tag);

	[LoggerMessage(Level = LogLevel.Warning, Message = "{Executable} exited {ExitCode}: {Error}")]
	private static partial void LogToolFailed(ILogger logger, string executable, string exitCode, string error);

	[LoggerMessage(Level = LogLevel.Warning, Message = "{Executable} outran its {Timeout} budget.")]
	private static partial void LogTimedOut(ILogger logger, string executable, TimeSpan timeout);

	[LoggerMessage(Level = LogLevel.Warning, Message = "{Executable} is not available; video is retained unstripped.")]
	private static partial void LogToolMissing(ILogger logger, string executable);

	[LoggerMessage(Level = LogLevel.Warning, Message = "Could not clear the remux workspace: {Reason}")]
	private static partial void LogWorkspaceNotCleared(ILogger logger, string reason);
}
