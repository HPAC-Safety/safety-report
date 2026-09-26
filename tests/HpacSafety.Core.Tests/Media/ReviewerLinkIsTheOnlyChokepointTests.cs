using System.Text.RegularExpressions;
using Shouldly;

namespace HpacSafety.Core.Tests.Media;

/// <summary>
///     <c>ReviewerMediaLink</c> is documented as the only sanctioned way to mint a
///     link to uploaded media — but <c>IBlobStore.CreateReadUrl</c> is a public
///     port with no guard of its own, and it will sign a GET for a report's
///     unstripped original as readily as for its derivative.
///     <para>
///         Convention is not enforcement. This walks the source of the shipping projects
///         and fails if anything outside the places allowed to call it does, so the
///         chokepoint is a rule rather than something a future contributor has to have
///         read.
///     </para>
///     <para>
///         A source scan rather than IL analysis: it is legible, it fails with a file
///         name a reviewer can open, and there is nothing clever in it to go wrong.
///     </para>
/// </summary>
public class ReviewerLinkIsTheOnlyChokepointTests
{
	[Theory]
	// PublicMediaLink forces a download too, but only of a public document's
	// original (ADR-0119).
	[InlineData("CreateReadUrl", "ReviewerMediaLink.cs,PublicMediaLink.cs")]
	[InlineData("CreateInlineReadUrl", "PublicMediaLink.cs")]
	// A reporter's one pre-signed PUT, to their upload's quarantine key (ADR-0126).
	[InlineData("CreateUploadUrl", "UploadLink.cs")]
	public void GivenShippingSource_WhenPresigningCallIsMade_ThenOnlyChokepointMakes(
		string method,
		string chokepointFiles)
	{
		// Given
		ArgumentNullException.ThrowIfNull(chokepointFiles);

		// The port itself declares the method, and the adapters implement it.
		// Everything else has to go through the chokepoint.
		var allowed = chokepointFiles.Split(',').Concat(["IBlobStore.cs", "S3BlobStore.cs"]).ToArray();
		var callSite = new Regex($@"\b{method}\s*\(", RegexOptions.None, TimeSpan.FromSeconds(5));

		// When
		var offenders = Directory
			.EnumerateFiles(Path.Combine(RepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
			.Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			.Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			.Where(path => !allowed.Contains(Path.GetFileName(path), StringComparer.Ordinal))
			.Where(path => callSite.IsMatch(File.ReadAllText(path)))
			.Select(path => Path.GetFileName(path))
			.ToArray();

		// Then
		offenders.ShouldBeEmpty(
			$"'{method}' may only be called from {chokepointFiles}. "
			+ "Signing a URL anywhere else bypasses the rule that a reviewer sees only stripped bytes "
			+ "and that an upload can only land in quarantine.");
	}

	[Fact]
	public void GivenChokepointItself_WhenSourceIsScanned_ThenScanIsFindingRealCallSites()
	{
		// Given
		var reviewerLink = Path.Combine(RepositoryRoot(), "src", "HpacSafety.Core", "Features", "Reporting", "ReviewerMediaLink.cs");

		// When
		var source = File.ReadAllText(reviewerLink);

		// Then
		// Guards the tests above: if the scan could not see a call it does not
		// matter that it saw none elsewhere. A guard that cannot fail is not a
		// guard.
		source.ShouldContain("CreateReadUrl(");
	}

	[Fact]
	public void GivenPublicChokepoint_WhenSourceIsScanned_ThenScanIsFindingRealCallSites()
	{
		// Given
		var publicLink = Path.Combine(RepositoryRoot(), "src", "HpacSafety.Core", "Features", "Reporting", "PublicMediaLink.cs");

		// When
		var source = File.ReadAllText(publicLink);

		// Then
		source.ShouldContain("CreateInlineReadUrl(");
	}

	[Fact]
	public void GivenUploadChokepoint_WhenSourceIsScanned_ThenScanIsFindingRealCallSites()
	{
		// Given
		var uploadLink = Path.Combine(RepositoryRoot(), "src", "HpacSafety.Core", "Features", "Reporting", "UploadLink.cs");

		// When
		var source = File.ReadAllText(uploadLink);

		// Then
		source.ShouldContain("CreateUploadUrl(");
	}

	internal static string RepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);

		while (directory is not null
			   && !File.Exists(Path.Combine(directory.FullName, "HpacSafety.slnx")))
		{
			directory = directory.Parent;
		}

		return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
	}
}
