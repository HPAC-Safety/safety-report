using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Infrastructure.Persistence.Seeding;

using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

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
    public void GivenGeneratedFormSpecification_WhenRead_ThenDescribesFieldsSeedReproduces()
    {
        // Given / When / Then — a parser that silently found nothing would make
        // every other assertion here vacuous.
        Spec.Count.ShouldBeGreaterThan(20);
        Spec.ShouldContain(f => f.Label == "Short form publication");
        Spec.ShouldContain(f => f.Choices.Count == 13);
    }

    [Fact]
    public void GivenFormSpecification_WhenSeedIsComparedTo_ThenEveryFieldIsSeededInOrder()
    {
        // Given
        var seeded = QuestionBankSeed.Questions;

        // When
        var labels = seeded.Select(q => q.LabelEn).ToArray();

        // Then
        labels.ShouldBe([.. Spec.Select(f => f.Label)]);
    }

    [Fact]
    public void GivenFormSpecification_WhenEachFieldTypeIsCompared_ThenSeedAsksForSameKindOfAnswer()
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
    public void GivenFormSpecification_WhenFieldSitsInsideGroup_ThenSeededQuestionSitsInSameSection()
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
    public void GivenFormSpecification_WhenFieldOffersChoices_ThenSeededQuestionOffersSameChoicesInSameOrder()
    {
        // Given
        var seeded = QuestionBankSeed.Questions;

        // When / Then
        foreach (var (field, question) in Spec.Zip(seeded))
        {
            question.Options.Select(o => o.LabelEn).ToArray()
                .ShouldBe([.. field.Choices], $"the choices on '{field.Label}'.");
        }
    }

    [Fact]
    public void GivenFormSpecification_WhenFieldCarriesHelpText_ThenSeededQuestionCarriesWordForWord()
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
    public void GivenSeededQuestionBank_WhenFrenchWordingIsChecked_ThenEveryQuestionHasCounterpart()
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
    public void GivenSeededQuestionBank_WhenKeysAreChecked_ThenEachOneIsUsedOnceAndIsAlreadyNormalized()
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
    public void GivenSeededQuestionBank_WhenOptionCodesAreChecked_ThenTheyAreUniqueWithinQuestionAndAlreadyNormalized()
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
    public void GivenSeededQuestionBank_WhenPublicationConsentIsLookedUp_ThenOnlySystemQuestionAndRequired()
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
    public void GivenSeededQuestionBank_WhenRoleIsAssigned_ThenNoTwoQuestionsClaimSameOne()
    {
        // Given
        var roles = QuestionBankSeed.Questions
            .Select(q => q.Role)
            .Where(role => role != QuestionRole.None)
            .ToArray();

        // When / Then
        roles.Distinct().Count().ShouldBe(roles.Length);
    }

    [Fact]
    public void GivenSeededQuestionBank_WhenPrivateIdentityFieldsAreRead_ThenEveryOneIsPrivate()
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
    public void GivenSeededQuestionBank_WhenSummaryContentFieldsAreRead_ThenEveryOneIsNonPrivate()
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
    public void GivenSeededQuestionBank_WhenRedactionContextFieldIsRead_ThenPrivate(string key)
    {
        QuestionBankSeed.Questions.Single(question => question.Key == key).IsPrivate.ShouldBeTrue();
    }

    [Fact]
    public void GivenSeededQuestionBank_WhenQuestionTakesNoOptions_ThenNoneAreSeededFor()
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

    [Fact]
    public void GivenCurrentQuestionSchema_WhenSeedIsWritten_ThenUsesImmutablePrivacyFlags()
    {
        // Given
        var migration = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        // When
        QuestionBankSeedWriter.Write(migration);

        // Then
        var operation = migration.Operations.ShouldHaveSingleItem().ShouldBeOfType<SqlOperation>();
        operation.Sql.ShouldContain("is_private");
        operation.Sql.ShouldNotContain("sensitivity");
    }
}
