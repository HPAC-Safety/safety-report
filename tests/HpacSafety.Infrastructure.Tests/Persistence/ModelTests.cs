using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Infrastructure.Persistence.Conventions;
using HpacSafety.Infrastructure.Persistence.Conversions;
using HpacSafety.Infrastructure.Persistence.Encryption;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Persistence;

/// <summary>
/// The model, without a database. These assert the mapping decisions a
/// migration then writes down. See ADR-0019.
/// </summary>
public sealed class ModelTests
{
    private static IModel Model()
    {
        using var context = Context();
        return context.Model;
    }

    private static HpacSafetyDbContext Context(string? key = null) =>
        new(
            new DbContextOptionsBuilder<HpacSafetyDbContext>()
                .UseNpgsql("Host=nowhere;Database=unused")
                .ReplaceService<IModelCacheKeyFactory, FieldCipherModelCacheKeyFactory>()
                .Options,
            PostgresFixture.CipherFor(key ?? PostgresFixture.Key));

    [Theory]
    [InlineData(typeof(Report), "reports")]
    [InlineData(typeof(ReportAnswer), "report_answers")]
    [InlineData(typeof(ReportAircraft), "report_aircraft")]
    [InlineData(typeof(ReportFile), "report_files")]
    [InlineData(typeof(Summary), "summaries")]
    [InlineData(typeof(Question), "questions")]
    [InlineData(typeof(QuestionVersion), "question_versions")]
    [InlineData(typeof(QuestionOption), "question_options")]
    [InlineData(typeof(QuestionTranslation), "question_translations")]
    [InlineData(typeof(QuestionOptionTranslation), "question_option_translations")]
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
        columns.ShouldContain("question_version_id");
        columns.ShouldContain("selected_option_codes");
        columns.ShouldContain("answered_at");
    }

    [Fact]
    public void Given_the_model_When_the_language_of_a_summary_is_mapped_Then_the_column_is_named_for_what_it_holds()
    {
        // Given / When
        var column = Model().FindEntityType(typeof(Summary))!.GetProperty(nameof(Summary.Locale)).GetColumnName();

        // Then — the issue names this column `language`.
        column.ShouldBe("language");
    }

    [Fact]
    public void Given_the_model_When_the_value_of_an_answer_is_mapped_Then_it_goes_through_the_cipher()
    {
        // Given / When
        var converter = Model().FindEntityType(typeof(ReportAnswer))!
            .GetProperty(nameof(ReportAnswer.Value)).GetValueConverter();

        // Then
        converter.ShouldBeOfType<EncryptedStringConverter>();
    }

    [Fact]
    public void Given_the_model_When_a_domain_enum_is_mapped_Then_it_is_stored_as_its_invariant_code()
    {
        // Given / When
        var converter = Model().FindEntityType(typeof(Report))!
            .GetProperty(nameof(Report.PilotInjury)).GetValueConverter();

        // Then
        converter.ShouldBeOfType<EnumCodeConverter<InjurySeverity>>();
        converter!.ConvertToProvider(InjurySeverity.Fatality).ShouldBe("fatality");
        converter.ConvertFromProvider("serious").ShouldBe(InjurySeverity.Serious);
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
        var converter = new EnumCodeConverter<InjurySeverity>();

        // When / Then
        Should.Throw<DomainRuleViolationException>(() => converter.ConvertFromProvider("mildly_startled"));
    }

    [Fact]
    public void Given_two_contexts_holding_different_keys_When_their_model_cache_keys_are_compared_Then_they_differ()
    {
        // Given
        var factory = new FieldCipherModelCacheKeyFactory();
        using var one = Context(PostgresFixture.Key);
        using var other = Context(PostgresFixture.OtherKey);

        // When / Then — sharing a cached model would mean the second context
        // silently read through the first one's key.
        factory.Create(one, designTime: false).ShouldNotBe(factory.Create(other, designTime: false));
    }

    [Fact]
    public void Given_two_contexts_holding_the_same_key_When_their_model_cache_keys_are_compared_Then_they_match()
    {
        // Given
        var factory = new FieldCipherModelCacheKeyFactory();
        using var one = Context();
        using var other = Context();

        // When / Then
        factory.Create(one, designTime: false).ShouldBe(factory.Create(other, designTime: false));
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

    [Fact]
    public void Given_a_context_built_without_a_cipher_When_it_is_created_Then_it_refuses()
    {
        // Given
        var options = new DbContextOptionsBuilder<HpacSafetyDbContext>().UseNpgsql("Host=nowhere").Options;

        // When / Then
        Should.Throw<ArgumentNullException>(() => new HpacSafetyDbContext(options, null!));
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
