using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Infrastructure.Persistence.Conventions;
using HpacSafety.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
///     The model, without a database. These assert the mapping decisions a
///     migration then writes down.
/// </summary>
public sealed class ModelTests
{
    private static IModel Model()
    {
        using var context = Context();
        return context.Model;
    }

    private static HpacSafetyDbContext Context()
    {
        return new HpacSafetyDbContext(new DbContextOptionsBuilder<HpacSafetyDbContext>().UseNpgsql("Host=nowhere;Database=unused").Options);
    }

    [Theory]
    [InlineData(typeof(Report), "reports")]
    [InlineData(typeof(ReportAnswer), "report_answers")]
    [InlineData(typeof(ReportFile), "report_files")]
    [InlineData(typeof(Summary), "summaries")]
    [InlineData(typeof(Question), "questions")]
    [InlineData(typeof(QuestionRevision), "question_revisions")]
    [InlineData(typeof(QuestionRevisionOption), "question_revision_options")]
    [InlineData(typeof(AuditLogEntry), "audit_log")]
    [InlineData(typeof(OutboxMessage), "outbox_messages")]
    public void GivenModel_WhenEntityIsMapped_ThenLandsInTableIssueNamed(Type entity, string table)
    {
        // Given / When
        var mapped = Model().FindEntityType(entity!);

        // Then
        mapped.ShouldNotBeNull();
        mapped.GetTableName().ShouldBe(table);
    }

    [Fact]
    public void GivenModel_WhenColumnIsNamed_ThenSnakeCase()
    {
        // Given
        var answer = Model().FindEntityType(typeof(ReportAnswer))!;

        // When
        var columns = answer.GetProperties().Select(p => p.GetColumnName()).ToArray();

        // Then
        columns.ShouldContain("question_revision_id");
        columns.ShouldContain("needs_translation");
        columns.ShouldContain("translated_value");
        columns.ShouldContain("answered_at");
        columns.ShouldContain("is_private");
    }

    [Fact]
    public void GivenModel_WhenDomainEnumIsMapped_ThenStoredAsInvariantCode()
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
    public void GivenModel_WhenLocaleIsMapped_ThenStoredAsCode()
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
    public void GivenStoredCodeNoLongerNamesDomainValue_WhenRead_ThenRefusedRatherThanGuessed()
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
    public void GivenName_WhenConverted_ThenReadsWayPostgresFolds(string name, string expected)
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
    [InlineData(typeof(OutboxMessage))]
    public void GivenEveryTableExceptAuditLog_WhenModelIsRead_ThenHasDeletedColumnAndLiveRowFilter(Type entity)
    {
        // Given / When
        var mapped = Model().FindEntityType(entity!)!;

        // Then
        mapped.FindProperty("Deleted").ShouldNotBeNull();
        mapped.GetDeclaredQueryFilters().ShouldNotBeEmpty();
    }

    [Fact]
    public void GivenAppendOnlyAuditLog_WhenModelIsRead_ThenHasNoDeletedColumn()
    {
        // Given / When
        var mapped = Model().FindEntityType(typeof(AuditLogEntry))!;

        // Then
        mapped.FindProperty("Deleted").ShouldBeNull();
        mapped.GetDeclaredQueryFilters().ShouldBeEmpty();
    }

    [Fact]
    public void GivenDesignTimeTooling_WhenAsksForContext_ThenGetsOneWithoutApplication()
    {
        // Given
        var factory = new HpacSafetyDbContextFactory();

        // When
        using var context = factory.CreateDbContext([]);

        // Then
        context.Model.FindEntityType(typeof(Report)).ShouldNotBeNull();
    }
}
