using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Anonymization.Tests;

public sealed class PrivateValueMarkerTests
{
	[Fact]
	public void GivenAnExactPrivateValueInReportContent_WhenMarked_ThenReplacedWithQuestionKeyedMarker()
	{
		// Given
		var input = SummarizationInput.Partition([
			new ClassifiedReportField(new SummarizationField("pilot_name", "Pilot name", "Ada Lovelace"), true),
			new ClassifiedReportField(new SummarizationField("description", "Description", "Ada Lovelace landed hard."), false)
		]);

		// When
		var marked = PrivateValueMarker.Mark(input);

		// Then
		marked.ReportContent.Single().Value.ShouldBe("[PRIVATE:pilot_name] landed hard.");
	}

	[Fact]
	public void GivenATokenFromAMultiWordPrivateValue_WhenMarked_ThenTokenIsAlsoReplaced()
	{
		// Given
		var input = SummarizationInput.Partition([
			new ClassifiedReportField(new SummarizationField("pilot_name", "Pilot name", "Ada Lovelace"), true),
			new ClassifiedReportField(new SummarizationField("description", "Description", "Lovelace radioed the tower."), false)
		]);

		// When
		var marked = PrivateValueMarker.Mark(input);

		// Then — "Lovelace" alone meets PrivateValueMarker.MinimumTokenLength (4) and is not a stopword.
		marked.ReportContent.Single().Value.ShouldBe("[PRIVATE:pilot_name] radioed the tower.");
	}

	[Fact]
	public void GivenAWordBelowTheMinimumTokenLength_WhenMarked_ThenLeftUnmarked()
	{
		// Given
		var input = SummarizationInput.Partition([
			new ClassifiedReportField(new SummarizationField("pilot_name", "Pilot name", "Ida Ng"), true),
			new ClassifiedReportField(new SummarizationField("description", "Description", "Ida taxied without clearance."), false)
		]);

		// When
		var marked = PrivateValueMarker.Mark(input);

		// Then — "Ida" and "Ng" are both below PrivateValueMarker.MinimumTokenLength (4), so neither
		// standalone token is marked; only an exact whole-value match would be.
		marked.ReportContent.Single().Value.ShouldBe("Ida taxied without clearance.");
	}

	[Fact]
	public void GivenAStopwordThatIsAlsoAPrivateValueToken_WhenMarked_ThenLeftUnmarked()
	{
		// Given
		var input = SummarizationInput.Partition([
			new ClassifiedReportField(new SummarizationField("site", "Site", "North Bay Airport"), true),
			new ClassifiedReportField(new SummarizationField("description", "Description", "The aircraft flew north before landing."), false)
		]);

		// When
		var marked = PrivateValueMarker.Mark(input);

		// Then — "north" is a curated stopword even though it meets the minimum length.
		marked.ReportContent.Single().Value.ShouldBe("The aircraft flew north before landing.");
	}

	[Fact]
	public void GivenAWholeMultiWordValueVerbatim_WhenMarked_ThenReplacedAsOneMarkerNotPerWord()
	{
		// Given
		var input = SummarizationInput.Partition([
			new ClassifiedReportField(new SummarizationField("pilot_name", "Pilot name", "Ada Lovelace"), true),
			new ClassifiedReportField(new SummarizationField("description", "Description", "Ada Lovelace reported the failure."), false)
		]);

		// When
		var marked = PrivateValueMarker.Mark(input);

		// Then
		marked.ReportContent.Single().Value.ShouldBe("[PRIVATE:pilot_name] reported the failure.");
		marked.ReportContent.Single().Value.ShouldNotContain("[PRIVATE:pilot_name] [PRIVATE:pilot_name]");
	}

	[Fact]
	public void GivenDifferentCasingAndExtraWhitespace_WhenMarked_ThenStillMatched()
	{
		// Given
		var input = SummarizationInput.Partition([
			new ClassifiedReportField(new SummarizationField("pilot_name", "Pilot name", "Ada Lovelace"), true),
			new ClassifiedReportField(new SummarizationField("description", "Description", "ADA   LOVELACE was flying."), false)
		]);

		// When
		var marked = PrivateValueMarker.Mark(input);

		// Then
		marked.ReportContent.Single().Value.ShouldBe("[PRIVATE:pilot_name] was flying.");
	}

	[Fact]
	public void GivenTheMarkingPass_WhenApplied_ThenPrivateContextIsUnchanged()
	{
		// Given
		var input = SummarizationInput.Partition([
			new ClassifiedReportField(new SummarizationField("pilot_name", "Pilot name", "Ada Lovelace"), true),
			new ClassifiedReportField(new SummarizationField("description", "Description", "Ada Lovelace reported the failure."), false)
		]);

		// When
		var marked = PrivateValueMarker.Mark(input);

		// Then
		marked.PrivateContext.ShouldBe(input.PrivateContext);
	}

	[Fact]
	public void GivenNoPrivateContext_WhenMarked_ThenReportContentIsUnchanged()
	{
		// Given
		var input = SummarizationInput.Partition([
			new ClassifiedReportField(new SummarizationField("description", "Description", "Nothing private here."), false)
		]);

		// When
		var marked = PrivateValueMarker.Mark(input);

		// Then
		marked.ReportContent.ShouldBe(input.ReportContent);
	}

	[Fact]
	public void GivenNoMatchInReportContent_WhenMarked_ThenValueUnchanged()
	{
		// Given
		var input = SummarizationInput.Partition([
			new ClassifiedReportField(new SummarizationField("pilot_name", "Pilot name", "Ada Lovelace"), true),
			new ClassifiedReportField(new SummarizationField("description", "Description", "The gear failed to retract."), false)
		]);

		// When
		var marked = PrivateValueMarker.Mark(input);

		// Then
		marked.ReportContent.Single().Value.ShouldBe("The gear failed to retract.");
	}

	[Fact]
	public void GivenNullInput_WhenMarked_ThenRejected()
	{
		// Given
		SummarizationInput input = null!;

		// When
		var act = () => PrivateValueMarker.Mark(input);

		// Then
		act.ShouldThrow<ArgumentNullException>();
	}
}
