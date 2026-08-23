using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;

using Shouldly;

namespace HpacSafety.Anonymization.Tests;

public sealed class SummarizationInputTests
{
    [Fact]
    public void Given_the_worker_query_dto_When_model_input_is_built_Then_answers_are_partitioned_by_question_privacy()
    {
        // Given
        var reportId = TinyId.New();
        var dto = new ReportForSummaryDto(
            reportId,
            Locale.EnCa,
            [
                new(TinyId.New(), "pilot_name", "Pilot name", IsPrivate: true, "Avery North"),
                new(TinyId.New(), "description", "Description", IsPrivate: false, "Avery North landed hard."),
            ]);

        // When
        var input = SummarizationInput.From(dto);

        // Then
        input.ReportId.ShouldBe(reportId);
        input.Language.ShouldBe(Locale.EnCa);
        input.PrivateContext.Select(field => field.QuestionKey).ShouldBe(["pilot_name"]);
        input.ReportContent.Select(field => field.QuestionKey).ShouldBe(["description"]);
    }

    [Fact]
    public void Given_a_skipped_question_When_model_input_is_built_Then_it_is_not_sent_to_the_model()
    {
        // Given
        var dto = new ReportForSummaryDto(
            TinyId.New(),
            Locale.FrCa,
            [new(TinyId.New(), "damage", "Dommages", IsPrivate: false, Answer: null)]);

        // When
        var input = SummarizationInput.From(dto);

        // Then
        input.ReportContent.ShouldBeEmpty();
        input.PrivateContext.ShouldBeEmpty();
    }

    [Fact]
    public void Given_the_model_boundary_When_ports_are_inspected_Then_only_one_summarizer_accepts_report_input()
    {
        // Given
        var assembly = typeof(ISummarizer).Assembly;

        // When
        var parameters = typeof(ISummarizer)
            .GetMethods()
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        // Then
        parameters.ShouldContain(typeof(SummarizationInput));
        assembly.GetType("HpacSafety.Core.Features.Reporting.IPiiAuditor").ShouldBeNull();
        assembly.GetType("HpacSafety.Core.SharedKernel.ITranslator").ShouldBeNull();
        assembly.GetType("HpacSafety.Core.Features.Reporting.IPublicationChannel").ShouldBeNull();
    }

    [Fact]
    public void Given_the_current_runtime_prompt_When_it_is_loaded_Then_it_defines_role_replacement_in_the_single_call()
    {
        // Given / When
        var prompt = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "summarize.v4.md"));

        // Then
        prompt.ShouldContain("the pilot landed hard");
        prompt.ShouldContain("Never keep any part of a matched name");
        prompt.ShouldContain("one short, anonymized summary");
        prompt.ShouldNotContain("{{include:");
        prompt.ShouldNotContain("PII auditor", Case.Insensitive);
    }
}
