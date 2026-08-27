using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Infrastructure.Persistence.Conventions;
using HpacSafety.Infrastructure.Persistence.Conversions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
/// The model, without a database. These assert the mapping decisions a
/// migration then writes down.
/// </summary>
public sealed class ModelTests
{
    private static IModel Model()
    {
        using var context = Context();
        return context.Model;
    }

    private static HpacSafetyDbContext Context() =>
        new(new DbContextOptionsBuilder<HpacSafetyDbContext>().UseNpgsql("Host=nowhere;Database=unused").Options);

    [Theory]
    [InlineData(typeof(Report), "reports")]
    [InlineData(typeof(ReportAnswer), "report_answers")]
    [InlineData(typeof(ReportFile), "report_files")]
    [InlineData(typeof(Summary), "summaries")]
    [InlineData(typeof(Question), "questions")]
    [InlineData(typeof(QuestionRevision), "question_revisions")]
    [InlineData(typeof(QuestionRevisionOption), "question_revision_options")]
    [InlineData(typeof(AdminUser), "admin_users")]
    [InlineData(typeof(AuditLogEntry), "audit_log")]
    [InlineData(typeof(OutboxMessage), "outbox_messages")]
    public void Given_the_model_When_an_entity_is_mapped_Then_it_lands_in_the_table_the_issue_named(Type entity, string table)
    {
        // Given / When
        var mapped = Model().FindEntityType(entity!);

        // Then
        mapped.ShouldNotBeNull();
        mapped.GetTableName().ShouldBe(table);
    }

    [Fact]
    public void Given_the_model_When_a_column_is_named_Then_it_is_snake_case()
    {
        // Given
        var answer = Model().FindEntityType(typeof(ReportAnswer))!;

        // When
        var columns = answer.GetProperties().Select(p => p.GetColumnName()).ToArray();

        // Then
        columns.ShouldContain("question_revision_id");
        columns.ShouldContain("selected_option_codes");
        columns.ShouldContain("answered_at");
        columns.ShouldContain("is_private");
    }

    [Fact]
    public void Given_the_model_When_a_domain_enum_is_mapped_Then_it_is_stored_as_its_invariant_code()
    {
        // Given / When
        var converter = Model().FindEntityType(typeof(Report))!
            .GetProperty(nameof(Report.Status)).GetValueConverter();

        // Then
        converter.ShouldBeOfType<EnumCodeConverter<ReportStatus>>();
        converter!.ConvertToProvider(ReportStatus.Published).ShouldBe("published");
        converter.ConvertFromProvider("approved").ShouldBe(ReportStatus.Approved);
    }

    [Fact]
    public void Given_the_model_When_a_locale_is_mapped_Then_it_is_stored_as_its_code()
    {
        // Given / When
        var converter = Model().FindEntityType(typeof(Report))!
            .GetProperty(nameof(Report.Language)).GetValueConverter();

        // Then
        converter.ShouldBeOfType<LocaleConverter>();
        converter!.ConvertToProvider(Locale.FrCa).ShouldBe("fr-CA");
        converter.ConvertFromProvider("en-CA").ShouldBe(Locale.EnCa);
    }

    [Fact]
    public void Given_a_stored_code_that_no_longer_names_a_domain_value_When_it_is_read_Then_it_is_refused_rather_than_guessed()
    {
        // Given
        var converter = new EnumCodeConverter<ReportStatus>();

        // When / Then
        Should.Throw<DomainRuleViolationException>(() => converter.ConvertFromProvider("mildly_startled"));
    }

    [Theory]
    [InlineData("ReportId", "report_id")]
    [InlineData("ExifStrippedAt", "exif_stripped_at")]
    [InlineData("PK_Reports", "pk_reports")]
    [InlineData("IX_ReportAnswers_ReportId", "ix_report_answers_report_id")]
    [InlineData("already_snake", "already_snake")]
    public void Given_a_name_When_it_is_converted_Then_it_reads_the_way_postgres_folds_it(string name, string expected)
    {
        // Given / When / Then
        SnakeCaseNames.ToSnakeCase(name).ShouldBe(expected);
    }

    [Theory]
    [InlineData(typeof(Report))]
    [InlineData(typeof(ReportAnswer))]
    [InlineData(typeof(ReportFile))]
    [InlineData(typeof(Summary))]
    [InlineData(typeof(Question))]
    [InlineData(typeof(QuestionRevision))]
    [InlineData(typeof(QuestionRevisionOption))]
    [InlineData(typeof(AdminUser))]
    [InlineData(typeof(OutboxMessage))]
    public void Given_every_table_except_audit_log_When_its_model_is_read_Then_it_has_a_deleted_column_and_a_live_row_filter(Type entity)
    {
        // Given / When
        var mapped = Model().FindEntityType(entity!)!;

        // Then
        mapped.FindProperty("Deleted").ShouldNotBeNull();
        mapped.GetDeclaredQueryFilters().ShouldNotBeEmpty();
    }

    [Fact]
    public void Given_the_append_only_audit_log_When_its_model_is_read_Then_it_has_no_deleted_column()
    {
        // Given / When
        var mapped = Model().FindEntityType(typeof(AuditLogEntry))!;

        // Then
        mapped.FindProperty("Deleted").ShouldBeNull();
        mapped.GetDeclaredQueryFilters().ShouldBeEmpty();
    }

    [Fact]
    public void Given_design_time_tooling_When_it_asks_for_a_context_Then_it_gets_one_without_an_application()
    {
        // Given
        var factory = new HpacSafetyDbContextFactory();

        // When
        using var context = factory.CreateDbContext([]);

        // Then
        context.Model.FindEntityType(typeof(Report)).ShouldNotBeNull();
    }
}
