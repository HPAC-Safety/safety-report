using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Infrastructure.Persistence.Seeding;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Seeding;

/// <summary>Checks the seed against the extracted Typeform specification.</summary>
public sealed class QuestionBankSeedTests
{
    private static readonly IReadOnlyList<FormSpec.Field> Spec = FormSpec.Fields();

    private static QuestionType Expected(FormSpec.Field field) => field.TypeCode switch
    {
        "statement" => QuestionType.Statement,
        "group" or "contact_info" => QuestionType.Group,
        "short_text" => QuestionType.ShortText,
        "long_text" => QuestionType.LongText,
        "phone_number" => QuestionType.Phone,
        "email" => QuestionType.Email,
        "date" => QuestionType.Date,
        "dropdown" => QuestionType.SingleSelect,
        "yes_no" => QuestionType.YesNo,
        "file_upload" => QuestionType.FileUpload,
        "multiple_choice" => field.IsMultiSelect ? QuestionType.MultiSelect : QuestionType.SingleSelect,
        _ => throw new InvalidOperationException($"Unknown Typeform field type '{field.TypeCode}'."),
    };

    [Fact]
    public void Given_the_extracted_form_When_it_is_read_Then_it_is_not_empty()
    {
        Spec.Count.ShouldBeGreaterThan(20);
        Spec.ShouldContain(field => field.Label == "Short form publication");
        Spec.ShouldContain(field => field.Choices.Count == 13);
    }

    [Fact]
    public void Given_the_extracted_form_When_the_seed_is_compared_Then_every_field_is_present_in_order()
    {
        QuestionBankSeed.Questions.Select(question => question.LabelEn)
            .ShouldBe(Spec.Select(field => field.Label));
    }

    [Fact]
    public void Given_the_extracted_form_When_field_types_are_compared_Then_each_input_shape_matches()
    {
        foreach (var (field, question) in Spec.Zip(QuestionBankSeed.Questions))
        {
            question.Type.ShouldBe(Expected(field), $"'{field.Label}' is a {field.TypeCode}.");
        }
    }

    [Fact]
    public void Given_the_extracted_form_When_sections_are_compared_Then_each_question_has_the_same_parent()
    {
        var keyOfGroup = Spec.Zip(QuestionBankSeed.Questions)
            .Where(pair => pair.First.TypeCode is "group" or "contact_info")
            .ToDictionary(pair => pair.First.Label, pair => pair.Second.Key, StringComparer.Ordinal);

        foreach (var (field, question) in Spec.Zip(QuestionBankSeed.Questions))
        {
            var expected = field.SectionLabel is null ? null : keyOfGroup[field.SectionLabel];
            question.SectionKey.ShouldBe(expected);
        }
    }

    [Fact]
    public void Given_the_extracted_form_When_choices_are_compared_Then_labels_and_order_match()
    {
        foreach (var (field, question) in Spec.Zip(QuestionBankSeed.Questions))
        {
            question.Options.Select(option => option.LabelEn).ShouldBe(field.Choices);
        }
    }

    [Fact]
    public void Given_the_seed_When_bilingual_content_is_checked_Then_every_display_value_is_present()
    {
        foreach (var question in QuestionBankSeed.Questions)
        {
            question.LabelEn.ShouldNotBeNullOrWhiteSpace();
            question.LabelFr.ShouldNotBeNullOrWhiteSpace();
            if (question.HelpEn is not null)
            {
                question.HelpFr.ShouldNotBeNullOrWhiteSpace();
            }

            question.Options.ShouldAllBe(option =>
                !string.IsNullOrWhiteSpace(option.LabelEn) && !string.IsNullOrWhiteSpace(option.LabelFr));
        }
    }

    [Fact]
    public void Given_the_seed_When_keys_and_option_codes_are_checked_Then_they_are_unique_and_normalized()
    {
        var keys = QuestionBankSeed.Questions.Select(question => question.Key).ToArray();
        keys.Distinct(StringComparer.Ordinal).Count().ShouldBe(keys.Length);

        foreach (var question in QuestionBankSeed.Questions)
        {
            QuestionKey.Normalize(question.Key).ShouldBe(question.Key);
            var codes = question.Options.Select(option => option.Code).ToArray();
            codes.Distinct(StringComparer.Ordinal).Count().ShouldBe(codes.Length);
            codes.ShouldAllBe(code => QuestionKey.Normalize(code) == code);
        }
    }

    [Fact]
    public void Given_the_seed_When_consent_is_checked_Then_it_is_the_only_required_system_question_by_stable_key()
    {
        var consent = QuestionBankSeed.Questions.Single(question => question.Key == QuestionKey.ConsentPublish);

        consent.Type.ShouldBe(QuestionType.YesNo);
        consent.IsPrivate.ShouldBeTrue();
        consent.IsActive.ShouldBeTrue();
        consent.Options.ShouldBeEmpty();
        QuestionBankSeed.Questions.Where(question => question.Key != QuestionKey.ConsentPublish)
            .ShouldAllBe(question => question.Key != QuestionKey.ConsentPublish);
    }

    [Fact]
    public void Given_the_seed_When_identity_and_narrative_fields_are_checked_Then_privacy_is_explicit()
    {
        string[] privateFields =
        [
            "reporter_first_name", "reporter_last_name", "reporter_phone", "reporter_email",
            "pilot_first_name", "pilot_last_name",
        ];
        string[] publicFields = ["damage", "description", "action_and_prevention"];

        privateFields.ShouldAllBe(key => QuestionBankSeed.Questions.Single(question => question.Key == key).IsPrivate);
        publicFields.ShouldAllBe(key => !QuestionBankSeed.Questions.Single(question => question.Key == key).IsPrivate);
    }

    [Fact]
    public void Given_the_seed_writer_When_sql_is_generated_Then_it_writes_complete_question_revisions_only()
    {
        var sql = QuestionBankSeedWriter.Sql();

        sql.ShouldContain("revision");
        sql.ShouldContain("label_en");
        sql.ShouldContain("label_fr");
        sql.ShouldContain("sort_order");
        sql.ShouldContain("is_private");
        sql.ShouldNotContain("question_versions");
        sql.ShouldNotContain("translation");
        sql.ShouldNotContain("question_role");
    }
}
