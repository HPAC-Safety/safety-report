using System.Diagnostics;
using System.Text.Json;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Media;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Media;

/// <summary>
///     The real toolchain, against real files. Everything else stands in a fake for
///     ffmpeg; this is the only place that proves the arguments in
///     <see cref="FfmpegVideoRemuxer" /> actually strip what ADR-0094 says they do.
///     <para>
///         Fixtures are generated here rather than committed: a synthetic clip from
///         ffmpeg's own test source, tagged with the fields a phone would write. No
///         real footage, and nothing to keep in the repository.
///     </para>
/// </summary>
[Trait("Category", "Integration")]
public sealed class FfmpegVideoRemuxerTests
{
	private static readonly string[] ProbeArguments =
		["-hide_banner", "-loglevel", "error", "-print_format", "json", "-show_format", "-show_streams"];

	[Fact]
	public async Task GivenClipWithLocationAndDeviceTags_WhenRemuxed_ThenDerivativeCarriesNone()
	{
		// Given — the tags an iPhone or Android writes into the container
		using var workspace = new Workspace();
		var source = await workspace.SyntheticClip(
			("location", "+49.2827-123.1207/"),
			("make", "Apple"),
			("model", "iPhone 15 Pro"),
			("comment", "Launch above the ridge"));

		// When
		var remuxed = await Remux(source);

		// Then
		remuxed.ShouldNotBeNull();
		var tags = await TagNames(remuxed);
		tags.ShouldNotContain(name => name.Contains("location", StringComparison.OrdinalIgnoreCase));
		tags.ShouldNotContain(name => name.Contains("make", StringComparison.OrdinalIgnoreCase));
		tags.ShouldNotContain(name => name.Contains("model", StringComparison.OrdinalIgnoreCase));
		tags.ShouldNotContain(name => name.Contains("comment", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task GivenClipWithADataTrack_WhenRemuxed_ThenOnlyAudioVisualStreamsSurvive()
	{
		// Given — an iPhone recording carries `mebx` timed-metadata tracks, which a
		// tag-based wipe leaves untouched while the file looks clean
		using var workspace = new Workspace();
		var source = await workspace.SyntheticClipWithDataTrack();

		// When
		var remuxed = await Remux(source);

		// Then
		remuxed.ShouldNotBeNull();
		var kinds = await StreamKinds(remuxed);
		kinds.ShouldNotBeEmpty();
		kinds.ShouldAllBe(kind => kind == "video" || kind == "audio");
	}

	[Fact]
	public async Task GivenClip_WhenRemuxed_ThenDerivativeIsNotAByteForByteCopy()
	{
		// Given — REQ-MED-007
		using var workspace = new Workspace();
		var source = await workspace.SyntheticClip(("location", "+49.2827-123.1207/"));

		// When
		var remuxed = await Remux(source);

		// Then
		remuxed.ShouldNotBeNull();
		remuxed.ShouldNotBe(await File.ReadAllBytesAsync(source));
	}

	[Fact]
	public async Task GivenBytesThatAreNotVideo_WhenRemuxed_ThenNothingIsProducedAndNothingIsWritten()
	{
		// Given — REQ-MED-015: a file ffmpeg cannot read is retained, not refused
		using var source = new MemoryStream("this is not a video"u8.ToArray());
		using var destination = new MemoryStream();

		// When
		var produced = await new FfmpegVideoRemuxer(NullLogger<FfmpegVideoRemuxer>.Instance)
			.TryRemux(source, destination, MediaType.Mp4, CancellationToken.None);

		// Then
		produced.ShouldBeFalse();
		destination.Length.ShouldBe(0);
	}

	[Fact]
	public async Task GivenNoToolchain_WhenRemuxed_ThenNothingIsProducedRatherThanThrowing()
	{
		// Given — REQ-MED-015: a deployment without ffmpeg still accepts video,
		// and retains it unstripped
		using var workspace = new Workspace();
		var source = await workspace.SyntheticClip();
		await using var reading = File.OpenRead(source);
		using var destination = new MemoryStream();

		// When
		var produced = await new FfmpegVideoRemuxer(
				NullLogger<FfmpegVideoRemuxer>.Instance, toolPrefix: "hpac-absent-")
			.TryRemux(reading, destination, MediaType.Mp4, CancellationToken.None);

		// Then
		produced.ShouldBeFalse();
		destination.Length.ShouldBe(0);
	}

	[Fact]
	public async Task GivenATimeoutItCannotMeet_WhenRemuxed_ThenNothingIsProduced()
	{
		// Given — a budget no real remux can meet
		using var workspace = new Workspace();
		var source = await workspace.SyntheticClip();
		await using var reading = File.OpenRead(source);
		using var destination = new MemoryStream();

		// When
		var produced = await new FfmpegVideoRemuxer(
				NullLogger<FfmpegVideoRemuxer>.Instance, TimeSpan.FromMilliseconds(1))
			.TryRemux(reading, destination, MediaType.Mp4, CancellationToken.None);

		// Then
		produced.ShouldBeFalse();
		destination.Length.ShouldBe(0);
	}

	[Fact]
	public async Task GivenCallerCancels_WhenRemuxed_ThenCancellationIsNotSwallowed()
	{
		// Given — a caller's cancellation is not the toolchain failing
		using var workspace = new Workspace();
		var source = await workspace.SyntheticClip();
		await using var reading = File.OpenRead(source);
		using var destination = new MemoryStream();
		using var cancelled = new CancellationTokenSource();
		await cancelled.CancelAsync();

		// When / Then
		await Should.ThrowAsync<OperationCanceledException>(() =>
			new FfmpegVideoRemuxer(NullLogger<FfmpegVideoRemuxer>.Instance)
				.TryRemux(reading, destination, MediaType.Mp4, cancelled.Token));
	}

	private static async Task<byte[]?> Remux(string path)
	{
		await using var source = File.OpenRead(path);
		using var destination = new MemoryStream();

		var produced = await new FfmpegVideoRemuxer(NullLogger<FfmpegVideoRemuxer>.Instance)
			.TryRemux(source, destination, MediaType.Mp4, CancellationToken.None);

		return produced ? destination.ToArray() : null;
	}

	private static async Task<IReadOnlyList<string>> TagNames(byte[] content)
	{
		var probe = await Probe(content);
		var names = new List<string>();

		foreach (var section in Sections(probe))
		{
			if (section.TryGetProperty("tags", out var tags)
				&& tags.ValueKind is JsonValueKind.Object)
			{
				names.AddRange(tags.EnumerateObject().Select(tag => tag.Name));
			}
		}

		return names;
	}

	private static async Task<IReadOnlyList<string>> StreamKinds(byte[] content)
	{
		var probe = await Probe(content);

		return probe.RootElement.TryGetProperty("streams", out var streams)
			?
			[
				.. streams.EnumerateArray().Select(stream =>
					stream.TryGetProperty("codec_type", out var kind) ? kind.GetString() ?? string.Empty : string.Empty),
			]
			: [];
	}

	private static IEnumerable<JsonElement> Sections(JsonDocument probe)
	{
		if (probe.RootElement.TryGetProperty("format", out var format))
		{
			yield return format;
		}

		if (!probe.RootElement.TryGetProperty("streams", out var streams))
		{
			yield break;
		}

		foreach (var stream in streams.EnumerateArray())
		{
			yield return stream;
		}
	}

	private static async Task<JsonDocument> Probe(byte[] content)
	{
		var path = Path.Combine(Path.GetTempPath(), $"hpac-probe-{Guid.NewGuid():N}.mp4");
		await File.WriteAllBytesAsync(path, content);

		try
		{
			return JsonDocument.Parse(await Tool("ffprobe", [.. ProbeArguments, path]));
		}
		finally
		{
			File.Delete(path);
		}
	}

	private static async Task<string> Tool(string executable,
										   IReadOnlyList<string> arguments)
	{
		var start = new ProcessStartInfo(executable)
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
		};

		foreach (var argument in arguments)
		{
			start.ArgumentList.Add(argument);
		}

		using var process = Process.Start(start)
							?? throw new InvalidOperationException($"{executable} could not be started.");

		var output = await process.StandardOutput.ReadToEndAsync();
		var error = await process.StandardError.ReadToEndAsync();
		await process.WaitForExitAsync();

		return process.ExitCode == 0
			? output
			: throw new InvalidOperationException($"{executable} exited {process.ExitCode}: {error}");
	}

	/// <summary>A temporary directory holding generated fixtures, removed afterwards.</summary>
	private sealed class Workspace : IDisposable
	{
		private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("hpac-remux-tests-");

		public void Dispose()
		{
			_directory.Delete(true);
		}

		/// <summary>Two seconds of ffmpeg's own test pattern, tagged as asked.</summary>
		public async Task<string> SyntheticClip(params (string Key, string Value)[] tags)
		{
			var path = Path.Combine(_directory.FullName, $"{Guid.NewGuid():N}.mp4");
			List<string> arguments =
			[
				"-nostdin", "-hide_banner", "-loglevel", "error", "-y",
				"-f", "lavfi", "-i", "testsrc=duration=2:size=160x120:rate=10",
				"-f", "lavfi", "-i", "sine=frequency=440:duration=2",
				"-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-c:a", "aac",
			];

			foreach (var (key, value) in tags)
			{
				arguments.Add("-metadata");
				arguments.Add($"{key}={value}");
			}

			arguments.Add(path);
			await Tool("ffmpeg", arguments);
			return path;
		}

		/// <summary>A clip carrying a timed-metadata track, the way a phone does.</summary>
		public async Task<string> SyntheticClipWithDataTrack()
		{
			var clip = await SyntheticClip();
			var subtitles = Path.Combine(_directory.FullName, "notes.srt");
			await File.WriteAllTextAsync(
				subtitles, "1\n00:00:00,000 --> 00:00:02,000\nLaunch site, pilot name\n\n");

			var path = Path.Combine(_directory.FullName, $"{Guid.NewGuid():N}.mp4");
			await Tool("ffmpeg",
			[
				"-nostdin", "-hide_banner", "-loglevel", "error", "-y",
				"-i", clip, "-i", subtitles,
				"-map", "0:v", "-map", "0:a", "-map", "1:0",
				"-c", "copy", "-c:s", "mov_text", path,
			]);

			return path;
		}
	}
}
