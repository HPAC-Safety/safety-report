using System.Text.RegularExpressions;
using Shouldly;

namespace HpacSafety.Core.Tests.Media;

/// <summary>
/// <c>ReviewerMediaLink</c> is documented as the only sanctioned way to mint a
/// link to uploaded media — but <c>IBlobStore.CreateReadUrlAsync</c> is a public
/// port with no guard of its own, and it will sign a GET for a report's
/// unstripped original as readily as for its derivative.
/// <para>
/// Convention is not enforcement. This walks the source of the shipping projects
/// and fails if anything outside the two places allowed to call it does, so the
/// chokepoint is a rule rather than something a future contributor has to have
/// read. Likewise for <c>CreateUploadUrlAsync</c>, whose chokepoint is
/// <c>MediaUploadSlot</c> — the thing that keeps every upload in quarantine.
/// </para>
/// <para>
/// A source scan rather than IL analysis: it is legible, it fails with a file
/// name a reviewer can open, and there is nothing clever in it to go wrong.
/// </para>
/// </summary>
public class ReviewerLinkIsTheOnlyChokepointTests
{
    [Theory]
    [InlineData("CreateReadUrlAsync", "ReviewerMediaLink.cs")]
    [InlineData("CreateUploadUrlAsync", "MediaUploadSlot.cs")]
    public void Given_the_shipping_source_When_a_presigning_call_is_made_Then_only_its_chokepoint_makes_it(
        string method,
        string chokepointFile)
    {
        // Given
        // The port itself declares the method, and the adapters implement it.
        // Everything else has to go through the chokepoint.
        var allowed = new[] { chokepointFile, "IBlobStore.cs", "S3BlobStore.cs", "FileSystemBlobStore.cs" };
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
            $"'{method}' may only be called from {chokepointFile}. "
            + "Signing a URL anywhere else bypasses the rule that a reviewer sees only stripped bytes "
            + "and that an upload can only land in quarantine.");
    }

    [Fact]
    public void Given_the_chokepoint_itself_When_the_source_is_scanned_Then_the_scan_is_finding_real_call_sites()
    {
        // Given
        var reviewerLink = Path.Combine(RepositoryRoot(), "src", "HpacSafety.Core", "Features", "Reporting", "ReviewerMediaLink.cs");

        // When
        var source = File.ReadAllText(reviewerLink);

        // Then
        // Guards the tests above: if the scan could not see a call it does not
        // matter that it saw none elsewhere. A guard that cannot fail is not a
        // guard.
        source.ShouldContain("CreateReadUrlAsync(");
    }

    internal static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HpacSafety.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
