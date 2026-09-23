namespace HpacSafety.Core;

/// <summary>
///     A private scratch file for bytes too large to hold in memory — an upload
///     being judged, an original being processed, a derivative being produced.
///     Deleted by the operating system when the stream is closed, whether the work
///     succeeded, failed, or was cancelled (#362).
/// </summary>
public static class TemporaryFile
{
	private const int BufferSize = 81920;

	/// <summary>Creates an empty, seekable, read-write file that deletes itself on close.</summary>
	public static FileStream Create()
	{
		// A random name, never one derived from a filename or key: nothing about
		// an attachment is written into the file system's namespace.
		var path = Path.Combine(Path.GetTempPath(), $"hpac-{Guid.NewGuid():N}");

		return new FileStream(
			path,
			FileMode.CreateNew,
			FileAccess.ReadWrite,
			FileShare.None,
			BufferSize,
			FileOptions.DeleteOnClose | FileOptions.Asynchronous);
	}
}
