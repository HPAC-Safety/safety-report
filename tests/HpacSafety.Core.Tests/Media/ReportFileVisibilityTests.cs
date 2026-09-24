using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests.Media;

/// <summary>
///     A reviewer hides a published report's image or video, and shows it again
///     (ADR-0117). A hide is a flag with who set it, never a change to the bytes.
/// </summary>
public class ReportFileVisibilityTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

	[Fact]
	public void GivenImage_WhenHiddenAndShown_ThenBothAreRecordedOnce()
	{
		// Given
		var file = File(MediaType.Jpeg);

		// When
		var hid = file.HideBy("officer-subject", Now);
		var hidAgain = file.HideBy("someone-else", Now.AddMinutes(1));

		// Then
		hid.ShouldBeTrue();
		hidAgain.ShouldBeFalse();
		file.HiddenAt.ShouldBe(Now);
		file.HiddenBySubject.ShouldBe("officer-subject");

		// When
		var showed = file.Show();
		var showedAgain = file.Show();

		// Then
		showed.ShouldBeTrue();
		showedAgain.ShouldBeFalse();
		file.HiddenAt.ShouldBeNull();
		file.HiddenBySubject.ShouldBeNull();
	}

	[Fact]
	public void GivenVideo_WhenHidden_ThenItIsHidden()
	{
		// Given
		var file = File(MediaType.Mp4);

		// When
		file.HideBy("officer-subject", Now);

		// Then
		file.HiddenAt.ShouldBe(Now);
	}

	[Fact]
	public void GivenDocument_WhenHiddenOrShown_ThenRefused()
	{
		// Given — a document is never public, so there is nothing to hide
		var file = File(MediaType.Pdf);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => file.HideBy("officer-subject", Now));
		Should.Throw<DomainRuleViolationException>(() => file.Show());
	}

	[Fact]
	public void GivenFileOfDeletedReport_WhenHidden_ThenRefused()
	{
		// Given
		var report = new Report(Locale.EnCa, Now);
		var file = report.AddFile(TinyId.New(), $"{report.Id}/original/{TinyId.New()}", MediaType.Jpeg.ContentType, 10, null, Now);
		report.SoftDelete(Now);

		// When / Then
		Should.Throw<DomainRuleViolationException>(() => file.HideBy("officer-subject", Now));
	}

	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	public void GivenBlankSubject_WhenHidden_ThenRefused(string subject)
	{
		// Given
		var file = File(MediaType.Jpeg);

		// When / Then
		Should.Throw<ArgumentException>(() => file.HideBy(subject, Now));
	}

	private static ReportFile File(MediaType type)
	{
		var report = new Report(Locale.EnCa, Now);
		return report.AddFile(TinyId.New(), $"{report.Id}/original/{TinyId.New()}", type.ContentType, 10, null, Now);
	}
}
