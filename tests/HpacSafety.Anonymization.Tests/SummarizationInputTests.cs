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
    public void Given_the_model_ports_When_their_parameters_are_inspected_Then_only_the_summarizer_accepts_report_input()
    {
        // Given / When
        var summarizerParameters = ParametersOf(typeof(ISummarizer));
        var auditorParameters = ParametersOf(typeof(IPiiAuditor));
        var translatorParameters = ParametersOf(typeof(HpacSafety.Core.SharedKernel.ITranslator));
        var publicationParameters = ParametersOf(typeof(IPublicationChannel));

        // Then
        summarizerParameters.ShouldContain(typeof(SummarizationInput));
        auditorParameters.ShouldNotContain(typeof(SummarizationInput));
        translatorParameters.ShouldNotContain(typeof(SummarizationInput));
        publicationParameters.ShouldNotContain(typeof(SummarizationInput));
    }

    private static Type[] ParametersOf(Type port) =>
        port.GetMethods().SelectMany(method => method.GetParameters()).Select(parameter => parameter.ParameterType).ToArray();
}
