using HpacSafety.Core.Features.Reporting;

using Shouldly;

namespace HpacSafety.Anonymization.Tests;

public sealed class SummarizationInputTests
{
    [Fact]
    public void Given_classified_answers_When_the_model_input_is_built_Then_private_fields_are_isolated_from_report_content()
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
    public void Given_private_context_When_the_model_input_is_built_Then_it_is_available_only_as_a_separate_section()
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
    public void Given_no_fields_When_the_model_input_is_built_Then_both_sections_are_empty()
    {
        // Given / When
        var input = SummarizationInput.Partition([]);

        // Then
        input.ReportContent.ShouldBeEmpty();
        input.PrivateContext.ShouldBeEmpty();
    }

    [Fact]
    public void Given_a_null_field_collection_When_the_model_input_is_built_Then_it_is_rejected()
    {
        // Given
        IEnumerable<ClassifiedReportField> fields = null!;

        // When
        var act = () => SummarizationInput.Partition(fields);

        // Then
        act.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Given_a_null_classified_field_When_the_model_input_is_built_Then_it_is_rejected()
    {
        // Given
        ClassifiedReportField[] fields = [null!];

        // When
        var act = () => SummarizationInput.Partition(fields);

        // Then
        act.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Given_a_classification_without_a_field_When_the_model_input_is_built_Then_it_is_rejected()
    {
        // Given
        ClassifiedReportField[] fields = [new(null!, IsPrivate: true)];

        // When
        var act = () => SummarizationInput.Partition(fields);

        // Then
        act.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Given_the_summarizer_port_When_its_parameters_are_inspected_Then_it_accepts_report_input()
    {
        // Given / When
        var summarizerParameters = ParametersOf(typeof(ISummarizer));

        // Then
        summarizerParameters.ShouldContain(typeof(SummarizationInput));
    }

    private static Type[] ParametersOf(Type port) =>
        port.GetMethods().SelectMany(method => method.GetParameters()).Select(parameter => parameter.ParameterType).ToArray();
}
