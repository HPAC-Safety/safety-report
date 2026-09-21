using HpacSafety.Core.Features.QuestionBank;

using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
/// The mutable side of the question bank: a reusable choice list an
/// administrator maintains. Its whole reason for being safe is that a revision
/// takes a copy — see <see cref="OptionSet.AsRevisionOptions"/> and ADR-0058.
/// </summary>
public class OptionSetTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private static OptionSet Aerodromes()
    {
        var set = OptionSet.Create("aerodromes", "Aerodromes", "Aérodromes", At);
        set.Add("golden", "Golden", "Golden");
        set.Add("lumby", "Lumby", "Lumby");
        return set;
    }

    [Fact]
    public void Given_a_new_set_When_it_is_created_Then_it_has_a_tiny_id_and_a_normalized_key()
    {
        // Given / When
        var set = OptionSet.Create("Aerodrome List", "Aerodromes", "Aérodromes", At);

        // Then
        set.Id.Value.Length.ShouldBe(TinyId.Length);
        set.Key.ShouldBe("aerodrome_list");
        set.Items.ShouldBeEmpty();
    }

    [Fact]
    public void Given_a_blank_name_When_a_set_is_created_Then_it_is_rejected()
    {
        // Given / When / Then — a set is named in both official languages or not at all
        Should.Throw<DomainRuleViolationException>(() => OptionSet.Create("aerodromes", " ", "Aérodromes", At));
        Should.Throw<DomainRuleViolationException>(() => OptionSet.Create("aerodromes", "Aerodromes", " ", At));
    }

    [Fact]
    public void Given_a_set_When_items_are_added_Then_they_keep_the_order_they_were_added_in()
    {
        // Given / When
        var set = Aerodromes();

        // Then
        set.Items.Select(item => item.Code).ShouldBe(["golden", "lumby"]);
        set.Items.Select(item => item.DisplayOrder).ShouldBe([0, 1]);
    }

    [Fact]
    public void Given_a_code_already_in_the_set_When_it_is_added_again_Then_it_is_rejected()
    {
        // Given
        var set = Aerodromes();

        // When / Then
        Should.Throw<DomainRuleViolationException>(() => set.Add("golden", "Golden", "Golden"));
    }

    [Fact]
    public void Given_a_removed_code_When_it_is_added_again_Then_the_original_item_is_revived()
    {
        // Given
        var set = Aerodromes();
        var original = set.Items.Single(item => item.Code == "lumby").Id;
        set.Remove("lumby", At.AddHours(1));

        // When
        var revived = set.Add("lumby", "Lumby (BC)", "Lumby (C.-B.)");

        // Then — the same row, so anything pointing at it still points somewhere
        revived.Id.ShouldBe(original);
        revived.LabelEn.ShouldBe("Lumby (BC)");
        set.Items.Select(item => item.Code).ShouldBe(["golden", "lumby"]);
    }

    [Fact]
    public void Given_a_set_When_it_is_renamed_Then_both_languages_change_and_a_blank_is_refused()
    {
        // Given
        var set = Aerodromes();

        // When
        set.Rename("Sites", "Sites de décollage");

        // Then
        set.NameEn.ShouldBe("Sites");
        set.NameFr.ShouldBe("Sites de décollage");
        Should.Throw<DomainRuleViolationException>(() => set.Rename("", "Sites"));
    }

    [Fact]
    public void Given_a_live_item_When_it_is_relabelled_Then_its_code_is_unchanged()
    {
        // Given
        var set = Aerodromes();

        // When
        set.Relabel("golden", "Golden (BC)", "Golden (C.-B.)");

        // Then — a relabel is wording, never a recode; history keeps pointing at 'golden'
        var item = set.Items.Single(candidate => candidate.Code == "golden");
        item.LabelEn.ShouldBe("Golden (BC)");
        item.Label(Locale.FrCa).ShouldBe("Golden (C.-B.)");
    }

    [Fact]
    public void Given_a_code_that_is_not_live_When_it_is_relabelled_Then_it_is_rejected()
    {
        // Given
        var set = Aerodromes();
        set.Remove("lumby", At.AddHours(1));

        // When / Then
        Should.Throw<DomainRuleViolationException>(() => set.Relabel("lumby", "Lumby", "Lumby"));
        Should.Throw<DomainRuleViolationException>(() => set.Relabel("pemberton", "Pemberton", "Pemberton"));
    }

    [Fact]
    public void Given_a_set_When_it_is_arranged_Then_the_items_take_the_given_order()
    {
        // Given
        var set = Aerodromes();
        set.Add("pemberton", "Pemberton", "Pemberton");

        // When
        set.Arrange(["pemberton", "golden", "lumby"]);

        // Then
        set.Items.Select(item => item.Code).ShouldBe(["pemberton", "golden", "lumby"]);
    }

    [Fact]
    public void Given_an_arrangement_that_is_not_the_whole_list_When_it_is_applied_Then_it_is_rejected()
    {
        // Given
        var set = Aerodromes();

        // When / Then — a partial order would leave the rest somewhere arbitrary
        Should.Throw<DomainRuleViolationException>(() => set.Arrange(["golden"]));
        Should.Throw<DomainRuleViolationException>(() => set.Arrange(["golden", "golden"]));
        Should.Throw<DomainRuleViolationException>(() => set.Arrange(["golden", "pemberton"]));
    }

    [Fact]
    public void Given_a_set_When_an_item_is_removed_Then_it_leaves_the_live_list_without_being_erased()
    {
        // Given
        var set = Aerodromes();

        // When
        set.Remove("lumby", At.AddHours(1));

        // Then
        set.Items.Select(item => item.Code).ShouldBe(["golden"]);
        Should.Throw<DomainRuleViolationException>(() => set.Remove("lumby", At.AddHours(2)));
    }

    [Fact]
    public void Given_a_set_When_it_is_deleted_Then_it_and_its_items_are_retired_and_it_refuses_further_edits()
    {
        // Given
        var set = Aerodromes();

        // When
        set.Delete(At.AddHours(1));

        // Then
        set.Deleted.ShouldBe(At.AddHours(1));
        set.Items.ShouldBeEmpty();
        Should.Throw<DomainRuleViolationException>(() => set.Add("pemberton", "Pemberton", "Pemberton"));
        Should.Throw<DomainRuleViolationException>(() => set.Rename("Sites", "Sites"));
        Should.Throw<DomainRuleViolationException>(() => set.Arrange(["golden"]));
        Should.Throw<DomainRuleViolationException>(() => set.Remove("golden", At.AddHours(2)));
    }

    [Fact]
    public void Given_a_deleted_set_When_it_is_deleted_again_Then_the_first_timestamp_stands()
    {
        // Given
        var set = Aerodromes();
        set.Delete(At.AddHours(1));

        // When
        set.Delete(At.AddHours(5));

        // Then
        set.Deleted.ShouldBe(At.AddHours(1));
    }

    [Fact]
    public void Given_a_blank_item_label_When_it_is_added_Then_it_is_rejected()
    {
        // Given
        var set = Aerodromes();

        // When / Then
        Should.Throw<DomainRuleViolationException>(() => set.Add("pemberton", " ", "Pemberton"));
        Should.Throw<DomainRuleViolationException>(() => set.Add("pemberton", "Pemberton", " "));
    }

    [Fact]
    public void Given_a_set_When_it_is_read_as_revision_options_Then_each_carries_its_source_item()
    {
        // Given
        var set = Aerodromes();

        // When
        var options = set.AsRevisionOptions();

        // Then
        options.Select(option => option.Code).ShouldBe(["golden", "lumby"]);
        options.Select(option => option.SourceItemId).ShouldBe([.. set.Items.Select(item => (TinyId?)item.Id)]);
    }
}
