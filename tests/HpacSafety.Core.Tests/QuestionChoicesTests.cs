using HpacSafety.Core.Features.QuestionBank;

using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
/// Which list a question renders, and what a reporter typing a missing value
/// does to a shared set — ADR-0063.
/// </summary>
public class QuestionChoicesTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

    private static OptionSet Sites()
    {
        var set = OptionSet.Create("sites", "Flying sites", "Sites de vol", At);
        set.Add("coopers", "Cooper's", "Cooper's");
        set.Add("woodside", "Woodside", "Woodside");
        return set;
    }

    private static Question BackedBy(OptionSet set, QuestionType type = QuestionType.Autocomplete) =>
        Question.Create(
            "where_did_this_happen", type, "Where did this happen?", "Où cela s'est-il produit ?", At,
            isActive: true, optionSetId: set.Id, options: set.AsRevisionOptions());

    [Fact]
    public void Given_a_type_ahead_When_the_list_grows_Then_it_offers_the_new_choice()
    {
        // Given
        var set = Sites();
        var question = BackedBy(set);

        // When
        set.Add("mount_7", "Mount 7", "Mont 7");

        // Then — the next pilot sees it without anyone republishing the question
        QuestionChoices.For(question.CurrentRevision, set)
            .Select(option => option.Code)
            .ShouldBe(["coopers", "woodside", "mount_7"]);
    }

    [Fact]
    public void Given_a_type_ahead_When_the_list_grows_Then_its_revision_still_records_what_was_shown()
    {
        // Given
        var set = Sites();
        var question = BackedBy(set);

        // When
        set.Add("mount_7", "Mount 7", "Mont 7");

        // Then — the record of what that reporter was offered is untouched
        QuestionChoices.Snapshot(question.CurrentRevision)
            .Select(option => option.Code)
            .ShouldBe(["coopers", "woodside"]);
    }

    [Theory]
    [InlineData(QuestionType.SingleSelect)]
    [InlineData(QuestionType.MultiSelect)]
    public void Given_a_closed_list_type_When_the_shared_list_grows_Then_it_still_renders_its_snapshot(QuestionType type)
    {
        // Given — a curated, closed set; showing an unmentioned choice would
        // make the revision's record misleading
        var set = Sites();
        var question = BackedBy(set, type);

        // When
        set.Add("mount_7", "Mount 7", "Mont 7");

        // Then
        QuestionChoices.RendersLiveSet(question.CurrentRevision, set).ShouldBeFalse();
        QuestionChoices.For(question.CurrentRevision, set)
            .Select(option => option.Code)
            .ShouldBe(["coopers", "woodside"]);
    }

    [Fact]
    public void Given_a_type_ahead_When_its_shared_list_is_retired_Then_it_falls_back_to_the_snapshot()
    {
        // Given
        var set = Sites();
        var question = BackedBy(set);

        // When
        set.Delete(At.AddHours(1));

        // Then — a retired set leaves the question showing what it recorded,
        // rather than showing nothing
        QuestionChoices.RendersLiveSet(question.CurrentRevision, set).ShouldBeFalse();
        QuestionChoices.For(question.CurrentRevision, set)
            .Select(option => option.Code)
            .ShouldBe(["coopers", "woodside"]);
    }

    [Fact]
    public void Given_no_shared_list_is_loaded_When_choices_are_resolved_Then_the_snapshot_is_used()
    {
        // Given
        var set = Sites();
        var question = BackedBy(set);

        // When / Then
        QuestionChoices.For(question.CurrentRevision, optionSet: null)
            .Select(option => option.Code)
            .ShouldBe(["coopers", "woodside"]);
    }

    [Fact]
    public void Given_a_different_list_When_choices_are_resolved_Then_the_snapshot_is_used()
    {
        // Given — a revision never renders a set it does not name
        var set = Sites();
        var question = BackedBy(set);
        var unrelated = OptionSet.Create("provinces", "Provinces", "Provinces", At);
        unrelated.Add("alberta", "Alberta", "Alberta");

        // When / Then
        QuestionChoices.RendersLiveSet(question.CurrentRevision, unrelated).ShouldBeFalse();
        QuestionChoices.For(question.CurrentRevision, unrelated)
            .Select(option => option.Code)
            .ShouldBe(["coopers", "woodside"]);
    }

    [Fact]
    public void Given_a_hand_typed_question_When_choices_are_resolved_Then_the_snapshot_is_used()
    {
        // Given — an autocomplete whose options were typed for it alone
        var question = Question.Create(
            "launch_site", QuestionType.Autocomplete, "Where from?", "D'où ?", At, isActive: true,
            options: [new QuestionOptionInput("golden", "Golden", "Golden")]);

        // When / Then
        QuestionChoices.For(question.CurrentRevision, optionSet: null)
            .Select(option => option.Code)
            .ShouldBe(["golden"]);
    }

    [Fact]
    public void Given_a_value_the_list_does_not_offer_When_a_reporter_submits_it_Then_it_is_added_and_marked()
    {
        // Given
        var set = Sites();

        // When
        var added = set.AddFromReporter("Mount 7", "Mount 7", "Mont 7");

        // Then
        added.Code.ShouldBe("mount_7");
        added.AddedByReporter.ShouldBeTrue();
        added.LabelEn.ShouldBe("Mount 7");
        added.Label(Locale.FrCa).ShouldBe("Mont 7");
        set.Items.Select(candidate => candidate.Code).ShouldBe(["coopers", "woodside", "mount_7"]);
    }

    [Fact]
    public void Given_a_value_already_offered_When_a_reporter_submits_it_Then_nothing_is_added_or_relabelled()
    {
        // Given — a reporter's spelling never overwrites an administrator's
        var set = Sites();

        // When
        var added = set.AddFromReporter("coopers", "coopers launch", "décollage coopers");

        // Then
        added.LabelEn.ShouldBe("Cooper's");
        added.AddedByReporter.ShouldBeFalse();
        set.Items.Count.ShouldBe(2);
    }

    [Fact]
    public void Given_two_reporters_naming_the_same_new_site_When_both_submit_Then_one_choice_exists()
    {
        // Given
        var set = Sites();

        // When — the second types it differently; both normalize to one code
        var first = set.AddFromReporter("Mount 7", "Mount 7", "Mont 7");
        var second = set.AddFromReporter("mount  7", "mount 7", "mont 7");

        // Then
        second.Id.ShouldBe(first.Id);
        second.LabelEn.ShouldBe("Mount 7");
        set.Items.Count(item => item.Code == "mount_7").ShouldBe(1);
    }

    [Fact]
    public void Given_a_choice_an_administrator_removed_When_a_reporter_retypes_it_Then_it_is_not_revived()
    {
        // Given — removal is the only curation tool there is
        var set = Sites();
        set.Remove("woodside", At.AddHours(1));

        // When
        var added = set.AddFromReporter("Woodside", "Woodside", "Woodside");

        // Then — the answer points at a real row, but the list still does not
        // offer it
        added.Code.ShouldBe("woodside");
        added.Deleted.ShouldNotBeNull();
        set.Items.Select(item => item.Code).ShouldBe(["coopers"]);
    }

    [Fact]
    public void Given_a_retired_list_When_a_reporter_adds_to_it_Then_it_is_refused()
    {
        // Given
        var set = Sites();
        set.Delete(At.AddHours(1));

        // When / Then
        Should.Throw<DomainRuleViolationException>(() => set.AddFromReporter("Mount 7", "Mount 7", "Mont 7"));
    }

    [Fact]
    public void Given_a_blank_label_When_a_reporter_adds_a_choice_Then_it_is_refused()
    {
        // Given — both official languages are required here as everywhere else
        var set = Sites();

        // When / Then
        Should.Throw<DomainRuleViolationException>(() => set.AddFromReporter("Mount 7", " ", "Mont 7"));
        Should.Throw<DomainRuleViolationException>(() => set.AddFromReporter("Mount 7", "Mount 7", " "));
    }

    [Fact]
    public void Given_an_administrator_adds_a_choice_When_it_is_read_Then_it_is_not_marked_reporter_added()
    {
        // Given / When
        var set = Sites();

        // Then
        set.Items.ShouldAllBe(item => !item.AddedByReporter);
    }

    [Fact]
    public void Given_a_reporter_added_choice_When_an_administrator_relabels_it_Then_the_marker_stays()
    {
        // Given — the flag records where a choice came from, not whether
        // anyone has touched it since
        var set = Sites();
        set.AddFromReporter("Mount 7", "mount 7", "mont 7");

        // When
        set.Relabel("mount_7", "Mount 7", "Mont 7");

        // Then
        var item = set.Items.Single(candidate => candidate.Code == "mount_7");
        item.LabelEn.ShouldBe("Mount 7");
        item.AddedByReporter.ShouldBeTrue();
    }
}
