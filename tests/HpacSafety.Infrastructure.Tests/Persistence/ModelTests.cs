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

/// <summary>Assertions for the intentionally small persistence model.</summary>
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
    [InlineData(typeof(Summary), "summaries")]
    [InlineData(typeof(Question), "questions")]
    [InlineData(typeof(QuestionOption), "question_options")]
    [InlineData(typeof(AdminUser), "admin_users")]
    [InlineData(typeof(AuditLogEntry), "audit_log")]
    [InlineData(typeof(OutboxMessage), "outbox_messages")]
    public void Given_the_model_When_an_entity_is_mapped_Then_it_lands_in_its_expected_table(Type entity, string table)
    {
        var mapped = Model().FindEntityType(entity);

        mapped.ShouldNotBeNull();
        mapped.GetTableName().ShouldBe(table);
    }

    [Fact]
    public void Given_an_answer_When_its_columns_are_mapped_Then_it_references_the_exact_question_revision()
    {
        var columns = Model().FindEntityType(typeof(ReportAnswer))!
            .GetProperties()
            .Select(property => property.GetColumnName())
            .ToArray();

        columns.ShouldContain("question_id");
        columns.ShouldContain("question_key");
        columns.ShouldContain("selected_option_codes");
        columns.ShouldContain("answered_at");
        columns.ShouldNotContain("question_version_id");
        columns.ShouldNotContain("is_private");
    }

    [Fact]
    public void Given_a_summary_When_its_language_is_mapped_Then_the_column_is_named_language()
    {
        var column = Model().FindEntityType(typeof(Summary))!
            .GetProperty(nameof(Summary.Locale))
            .GetColumnName();

        column.ShouldBe("language");
    }

    [Fact]
    public void Given_an_answer_When_its_value_is_mapped_Then_it_is_encrypted()
    {
        var converter = Model().FindEntityType(typeof(ReportAnswer))!
            .GetProperty(nameof(ReportAnswer.Value))
            .GetValueConverter();

        converter.ShouldBeOfType<EncryptedStringConverter>();
    }

    [Fact]
    public void Given_a_question_type_When_it_is_mapped_Then_it_uses_an_invariant_code()
    {
        var converter = Model().FindEntityType(typeof(Question))!
            .GetProperty(nameof(Question.Type))
            .GetValueConverter();

        converter.ShouldBeOfType<EnumCodeConverter<QuestionType>>();
        converter!.ConvertToProvider(QuestionType.LongText).ShouldBe("long_text");
        converter.ConvertFromProvider("yes_no").ShouldBe(QuestionType.YesNo);
    }

    [Fact]
    public void Given_a_locale_When_it_is_mapped_Then_it_uses_its_code()
    {
        var converter = Model().FindEntityType(typeof(Report))!
            .GetProperty(nameof(Report.Language))
            .GetValueConverter();

        converter.ShouldBeOfType<LocaleConverter>();
        converter!.ConvertToProvider(Locale.FrCa).ShouldBe("fr-CA");
        converter.ConvertFromProvider("en-CA").ShouldBe(Locale.EnCa);
    }

    [Fact]
    public void Given_an_unknown_stored_enum_code_When_it_is_read_Then_it_is_rejected()
    {
        var converter = new EnumCodeConverter<QuestionType>();

        Should.Throw<DomainRuleViolationException>(() => converter.ConvertFromProvider("mystery_input"));
    }

    [Fact]
    public void Given_two_contexts_with_different_keys_When_model_cache_keys_are_compared_Then_they_differ()
    {
        var factory = new FieldCipherModelCacheKeyFactory();
        using var one = Context(PostgresFixture.Key);
        using var other = Context(PostgresFixture.OtherKey);

        factory.Create(one, designTime: false).ShouldNotBe(factory.Create(other, designTime: false));
    }

    [Fact]
    public void Given_two_contexts_with_the_same_key_When_model_cache_keys_are_compared_Then_they_match()
    {
        var factory = new FieldCipherModelCacheKeyFactory();
        using var one = Context();
        using var other = Context();

        factory.Create(one, designTime: false).ShouldBe(factory.Create(other, designTime: false));
    }

    [Theory]
    [InlineData("ReportId", "report_id")]
    [InlineData("SupersedesQuestionId", "supersedes_question_id")]
    [InlineData("PK_Reports", "pk_reports")]
    [InlineData("IX_ReportAnswers_ReportId", "ix_report_answers_report_id")]
    [InlineData("already_snake", "already_snake")]
    public void Given_a_name_When_it_is_converted_Then_it_is_snake_case(string name, string expected) =>
        SnakeCaseNames.ToSnakeCase(name).ShouldBe(expected);

    [Fact]
    public void Given_a_context_without_a_cipher_When_it_is_created_Then_it_is_rejected()
    {
        var options = new DbContextOptionsBuilder<HpacSafetyDbContext>().UseNpgsql("Host=nowhere").Options;

        Should.Throw<ArgumentNullException>(() => new HpacSafetyDbContext(options, null!));
    }

    [Fact]
    public void Given_design_time_tooling_When_it_requests_a_context_Then_the_model_is_available()
    {
        using var context = new HpacSafetyDbContextFactory().CreateDbContext([]);

        context.Model.FindEntityType(typeof(Report)).ShouldNotBeNull();
    }
}
