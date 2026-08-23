using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using HpacSafety.Infrastructure.Persistence.Seeding;

using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Seeding;

/// <summary>
/// The seeded question bank has to reproduce every field in
/// <c>docs/form-spec.md</c>, or a clean database asks a different form from the
/// one HPAC has been collecting. These read the spec rather than a second copy
/// of it, so drift fails here instead of in production.
/// </summary>
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
        _ => throw new InvalidOperationException($"docs/form-spec.md uses a field type this test does not know: '{field.TypeCode}'."),
    };

    [Fact]
    public void Given_the_generated_form_specification_When_it_is_read_Then_it_describes_the_fields_the_seed_reproduces()
    {
        // Given / When / Then — a parser that silently found nothing would make
        // every other assertion here vacuous.
        Spec.Count.ShouldBeGreaterThan(20);
        Spec.ShouldContain(f => f.Label == "Short form publication");
        Spec.ShouldContain(f => f.Choices.Count == 13);
    }

    [Fact]
    public void Given_the_form_specification_When_the_seed_is_compared_to_it_Then_every_field_is_seeded_in_order()
    {
        // Given
        var seeded = QuestionBankSeed.Questions;

        // When
        var labels = seeded.Select(q => q.LabelEn).ToArray();

        // Then
        labels.ShouldBe(Spec.Select(f => f.Label).ToArray());
    }

    [Fact]
    public void Given_the_form_specification_When_each_field_type_is_compared_Then_the_seed_asks_for_the_same_kind_of_answer()
    {
        // Given
        var seeded = QuestionBankSeed.Questions;

        // When / Then
        foreach (var (field, question) in Spec.Zip(seeded))
        {
            question.Type.ShouldBe(Expected(field), $"'{field.Label}' is a {field.TypeCode} in docs/form-spec.md.");
        }
    }

    [Fact]
    public void Given_the_form_specification_When_a_field_sits_inside_a_group_Then_the_seeded_question_sits_in_the_same_section()
    {
        // Given
        var seeded = QuestionBankSeed.Questions;
        var keyOfGroup = Spec.Zip(seeded)
            .Where(pair => pair.First.TypeCode is "group" or "contact_info")
            .ToDictionary(pair => pair.First.Label, pair => pair.Second.Key, StringComparer.Ordinal);

        // When / Then
        foreach (var (field, question) in Spec.Zip(seeded))
        {
            var expected = field.SectionLabel is null ? null : keyOfGroup[field.SectionLabel];
            question.SectionKey.ShouldBe(expected, $"'{field.Label}' belongs to '{field.SectionLabel ?? "no section"}'.");
        }
    }

    [Fact]
    public void Given_the_form_specification_When_a_field_offers_choices_Then_the_seeded_question_offers_the_same_choices_in_the_same_order()
    {
        // Given
        var seeded = QuestionBankSeed.Questions;

        // When / Then
        foreach (var (field, question) in Spec.Zip(seeded))
        {
            question.Options.Select(o => o.LabelEn).ToArray()
                .ShouldBe(field.Choices.ToArray(), $"the choices on '{field.Label}'.");
        }
    }

    [Fact]
    public void Given_the_form_specification_When_a_field_carries_help_text_Then_the_seeded_question_carries_it_word_for_word()
    {
        // Given — the recap screen's body is a list of Typeform field
        // references, which mean nothing outside Typeform and are deliberately
        // not copied. Everything else is transcribed exactly.
        var seeded = QuestionBankSeed.Questions;

        // When / Then
        foreach (var (field, question) in Spec.Zip(seeded))
        {
            if (field.Help is null || field.Help.Contains("{{field:", StringComparison.Ordinal))
            {
                continue;
            }

            question.HelpEn.ShouldBe(field.Help, $"the help text on '{field.Label}'.");
        }
    }

    [Fact]
    public void Given_the_seeded_question_bank_When_the_French_wording_is_checked_Then_every_question_has_a_counterpart()
    {
        // Given / When / Then — a question cannot be activated with a missing
        // counterpart, so a clean database would render an empty form.
        foreach (var question in QuestionBankSeed.Questions)
        {
            question.LabelFr.ShouldNotBeNullOrWhiteSpace($"the French label for '{question.Key}'.");
            question.LabelFr.ShouldNotBe(question.LabelEn, $"the French label for '{question.Key}'.");

            if (question.HelpEn is not null)
            {
                question.HelpFr.ShouldNotBeNullOrWhiteSpace($"the French help text for '{question.Key}'.");
            }

            foreach (var option in question.Options)
            {
                option.LabelFr.ShouldNotBeNullOrWhiteSpace($"the French label for option '{option.Code}'.");
            }
        }
    }

    [Fact]
    public void Given_the_seeded_question_bank_When_the_keys_are_checked_Then_each_one_is_used_once_and_is_already_normalized()
    {
        // Given
        var keys = QuestionBankSeed.Questions.Select(q => q.Key).ToArray();

        // When / Then
        keys.Distinct(StringComparer.Ordinal).Count().ShouldBe(keys.Length);
        foreach (var key in keys)
        {
            QuestionKey.Normalize(key).ShouldBe(key);
        }
    }

    [Fact]
    public void Given_the_seeded_question_bank_When_option_codes_are_checked_Then_they_are_unique_within_a_question_and_already_normalized()
    {
        // Given / When / Then
        foreach (var question in QuestionBankSeed.Questions)
        {
            var codes = question.Options.Select(o => o.Code).ToArray();
            codes.Distinct(StringComparer.Ordinal).Count().ShouldBe(codes.Length, $"the option codes on '{question.Key}'.");

            foreach (var code in codes)
            {
                QuestionKey.Normalize(code).ShouldBe(code);
            }
        }
    }

    [Fact]
    public void Given_the_seeded_question_bank_When_publication_consent_is_looked_up_Then_it_is_the_only_system_question_and_it_is_required()
    {
        // Given
        var system = QuestionBankSeed.Questions.Where(q => q.IsSystem).ToArray();

        // When / Then
        system.Length.ShouldBe(1);
        system[0].Key.ShouldBe(QuestionKey.ConsentPublish);
        system[0].Type.ShouldBe(QuestionType.YesNo);
        system[0].Role.ShouldBe(QuestionRole.ConsentPublish);
        system[0].IsRequired.ShouldBeTrue();
        system[0].Options.ShouldBeEmpty();
    }

    [Fact]
    public void Given_the_seeded_question_bank_When_a_role_is_assigned_Then_no_two_questions_claim_the_same_one()
    {
        // Given
        var roles = QuestionBankSeed.Questions
            .Select(q => q.Role)
            .Where(role => role != QuestionRole.None)
            .ToArray();

        // When / Then
        roles.Distinct().Count().ShouldBe(roles.Length);
    }

    [Theory]
    [InlineData("province", typeof(Province))]
    [InlineData("pilot_injury", typeof(InjurySeverity))]
    [InlineData("passenger_injury", typeof(InjurySeverity))]
    [InlineData("time_of_day", typeof(TimeOfDay))]
    public void Given_a_seeded_question_whose_answer_projects_onto_the_report_When_its_codes_are_read_Then_each_one_resolves_to_a_domain_value(
        string key, Type domainEnum)
    {
        ArgumentNullException.ThrowIfNull(domainEnum);

        // Given
        var question = QuestionBankSeed.Questions.Single(q => q.Key == key);

        // When / Then — a code that does not resolve means the projection in
        // Report quietly leaves the property at NotAnswered.
        foreach (var option in question.Options)
        {
            var resolved = Enum.GetValues(domainEnum)
                .Cast<Enum>()
                .Any(value => string.Equals(EnumCodeOf(value), option.Code, StringComparison.Ordinal));

            resolved.ShouldBeTrue($"'{option.Code}' on '{key}' does not name a {domainEnum.Name}.");
        }
    }

    [Fact]
    public void Given_the_seeded_question_bank_When_private_identity_fields_are_read_Then_every_one_is_private()
    {
        // Given
        string[] contact =
        [
            "reporter_first_name", "reporter_last_name", "reporter_phone", "reporter_email",
            "pilot_first_name", "pilot_last_name",
        ];

        // When / Then
        foreach (var key in contact)
        {
            QuestionBankSeed.Questions.Single(q => q.Key == key)
                .IsPrivate.ShouldBeTrue($"the privacy classification of '{key}'.");
        }
    }

    [Fact]
    public void Given_the_seeded_question_bank_When_summary_content_fields_are_read_Then_every_one_is_non_private()
    {
        // Given
        string[] reportContent =
        [
            "time_of_day", "in_canada", "province", "aircraft_type", "aircraft_certification",
            "pilot_injury", "passenger_injury", "injury_description", "damage", "description",
            "action_and_prevention",
        ];

        // When / Then
        foreach (var key in reportContent)
        {
            QuestionBankSeed.Questions.Single(q => q.Key == key)
                .IsPrivate.ShouldBeFalse($"the privacy classification of '{key}'.");
        }
    }

    [Theory]
    [InlineData("occurrence_date")]
    [InlineData("pilot_ratings")]
    [InlineData("location")]
    [InlineData("aircraft_manufacturer")]
    [InlineData("aircraft_model")]
    [InlineData("photo_or_video")]
    [InlineData(QuestionKey.ConsentPublish)]
    public void Given_the_seeded_question_bank_When_a_redaction_context_field_is_read_Then_it_is_private(string key)
    {
        QuestionBankSeed.Questions.Single(question => question.Key == key).IsPrivate.ShouldBeTrue();
    }

    [Fact]
    public void Given_the_seeded_question_bank_When_a_question_takes_no_options_Then_none_are_seeded_for_it()
    {
        // Given / When / Then
        foreach (var question in QuestionBankSeed.Questions)
        {
            var takesOptions = question.Type is QuestionType.SingleSelect or QuestionType.MultiSelect;

            if (!takesOptions)
            {
                question.Options.ShouldBeEmpty($"'{question.Key}' is a {question.Type}.");
            }
            else
            {
                question.Options.ShouldNotBeEmpty($"'{question.Key}' is a {question.Type}.");
            }
        }
    }

    private static string EnumCodeOf(Enum value) =>
        (string)typeof(EnumCode)
            .GetMethod(nameof(EnumCode.Of))!
            .MakeGenericMethod(value.GetType())
            .Invoke(null, [value])!;
}
