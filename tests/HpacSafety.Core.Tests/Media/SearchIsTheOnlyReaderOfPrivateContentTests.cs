using System.Text.RegularExpressions;
using Shouldly;

namespace HpacSafety.Core.Tests.Media;

/// <summary>
///     <c>HpacSafetyDbContext.SearchAdminReports</c> is the one sanctioned way to
///     query <c>search_admin_reports(query)</c> — and, through it, the one
///     sanctioned reader of <c>admin_report_search_document</c>, the reviewed
///     exception ADR-0156 carves into ADR-0133 and ADR-0135's "no database view
///     reads it" (#573).
///     <para>
///         Convention is not enforcement. This walks the source of the shipping
///         projects and fails if anything outside <c>ReportEndpoints.cs</c> calls
///         it, so the exception stays exactly as narrow as the ADR says: reachable
///         only through the reviewer-gated <c>GET /api/admin/reports?q=</c>.
///     </para>
///     <para>
///         A source scan rather than IL analysis, mirroring
///         <see cref="ReviewerLinkIsTheOnlyChokepointTests" />: legible, fails with
///         a file name a reviewer can open, nothing clever to go wrong.
///     </para>
/// </summary>
public class SearchIsTheOnlyReaderOfPrivateContentTests
{
	[Fact]
	public void GivenShippingSource_WhenSearchAdminReportsIsCalled_ThenOnlyReportEndpointsCalls()
	{
		// Given
		var allowed = new[] { "HpacSafetyDbContext.cs", "ReportEndpoints.cs" };
		var callSite = new Regex(@"\bSearchAdminReports\s*\(", RegexOptions.None, TimeSpan.FromSeconds(5));

		// When
		var offenders = Directory
			.EnumerateFiles(Path.Combine(ReviewerLinkIsTheOnlyChokepointTests.RepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
			.Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			.Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			.Where(path => !allowed.Contains(Path.GetFileName(path), StringComparer.Ordinal))
			.Where(path => callSite.IsMatch(File.ReadAllText(path)))
			.Select(Path.GetFileName)
			.ToArray();

		// Then
		offenders.ShouldBeEmpty(
			"'SearchAdminReports' — and the private-note/private-attachment text it reads through "
			+ "admin_report_search_document — may only be called from ReportEndpoints.cs, behind "
			+ "HpacPolicies.Reviewer (SafetyOfficer/Administrator). Calling it anywhere else would "
			+ "widen ADR-0156's exception to ADR-0133/ADR-0135 beyond the one reviewer-gated endpoint.");
	}

	[Fact]
	public void GivenReportEndpoints_WhenSourceIsScanned_ThenScanIsFindingRealCallSites()
	{
		// Given
		var reportEndpoints = Path.Combine(
			ReviewerLinkIsTheOnlyChokepointTests.RepositoryRoot(), "src", "HpacSafety.Api", "Admin", "ReportEndpoints.cs");

		// When
		var source = File.ReadAllText(reportEndpoints);

		// Then
		// Guards the test above: if the scan could not see a call here, it does
		// not matter that it saw none elsewhere.
		source.ShouldContain("SearchAdminReports(");
	}

	[Fact]
	public void GivenTheReportsGroup_WhenSourceIsScanned_ThenItRequiresTheReviewerPolicy()
	{
		// Given
		var reportEndpoints = Path.Combine(
			ReviewerLinkIsTheOnlyChokepointTests.RepositoryRoot(), "src", "HpacSafety.Api", "Admin", "ReportEndpoints.cs");

		// When
		var source = File.ReadAllText(reportEndpoints);

		// Then — the one endpoint that may reach the search view is gated the
		// same way as every other admin report route.
		source.ShouldContain("RequireAuthorization(HpacPolicies.Reviewer)");
	}
}
