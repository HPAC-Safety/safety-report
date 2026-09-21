using HpacSafety.Core.Features.Reporting;

using Shouldly;

namespace HpacSafety.Anonymization.Tests;

public sealed class SummarizationInputTests
{
    [Fact]
    public void GivenClassifiedAnswers_WhenModelInputIsBuilt_ThenPrivateFieldsAreIsolatedFromReportContent()
    {
        // Given
        ClassifiedReportField[] fields =
        [
            new(new SummarizationField("pilot_name", "Pilot name", "Ada Lovelace"), IsPrivate: true),
            new(new SummarizationField("description", "Description", "Ada Lovelace landed hard."), IsPrivate: false),
        ];

        // When
        var input = SummarizationInput.Partition(fields);

        // Then
        input.ReportContent.Select(field => field.QuestionKey).ShouldBe(["description"]);
        input.PrivateContext.Select(field => field.QuestionKey).ShouldBe(["pilot_name"]);
    }

    [Fact]
    public void GivenPrivateContext_WhenModelInputIsBuilt_ThenAvailableOnlyAsSeparateSection()
    {
        // Given
        var privateName = new SummarizationField("pilot_name", "Pilot name", "Ada Lovelace");

        // When
        var input = SummarizationInput.Partition([new(privateName, IsPrivate: true)]);

        // Then
        input.ReportContent.ShouldBeEmpty();
        input.PrivateContext.ShouldBe([privateName]);
    }

    [Fact]
    public void GivenNoFields_WhenModelInputIsBuilt_ThenBothSectionsAreEmpty()
    {
        // Given / When
        var input = SummarizationInput.Partition([]);

        // Then
        input.ReportContent.ShouldBeEmpty();
        input.PrivateContext.ShouldBeEmpty();
    }

    [Fact]
    public void GivenNullFieldCollection_WhenModelInputIsBuilt_ThenRejected()
    {
        // Given
        IEnumerable<ClassifiedReportField> fields = null!;

        // When
        var act = () => SummarizationInput.Partition(fields);

        // Then
        act.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void GivenNullClassifiedField_WhenModelInputIsBuilt_ThenRejected()
    {
        // Given
        ClassifiedReportField[] fields = [null!];

        // When
        var act = () => SummarizationInput.Partition(fields);

        // Then
        act.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void GivenClassificationWithoutField_WhenModelInputIsBuilt_ThenRejected()
    {
        // Given
        ClassifiedReportField[] fields = [new(null!, IsPrivate: true)];

        // When
        var act = () => SummarizationInput.Partition(fields);

        // Then
        act.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void GivenSummarizerPort_WhenParametersAreInspected_ThenAcceptsReportInput()
    {
        // Given / When
        var summarizerParameters = ParametersOf(typeof(ISummarizer));

        // Then
        summarizerParameters.ShouldContain(typeof(SummarizationInput));
    }

    private static Type[] ParametersOf(Type port) =>
        [.. port.GetMethods().SelectMany(method => method.GetParameters()).Select(parameter => parameter.ParameterType)];
}
