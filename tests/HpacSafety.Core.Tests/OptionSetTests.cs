using HpacSafety.Core.Features.QuestionBank;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     The mutable side of the question bank: a reusable choice list an
///     administrator maintains. Its whole reason for being safe is that a revision
///     takes a copy — see <see cref="OptionSet.AsRevisionOptions" /> and ADR-0058.
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
    public void GivenNewSet_WhenCreated_ThenHasTinyIdAndNormalizedKey()
    {
        // Given / When
        var set = OptionSet.Create("Aerodrome List", "Aerodromes", "Aérodromes", At);

        // Then
        set.Id.Value.Length.ShouldBe(TinyId.Length);
        set.Key.ShouldBe("aerodrome_list");
        set.Items.ShouldBeEmpty();
    }

    [Fact]
    public void GivenBlankName_WhenSetIsCreated_ThenRejected()
    {
        // Given / When / Then — a set is named in both official languages or not at all
        Should.Throw<DomainRuleViolationException>(() => OptionSet.Create("aerodromes", " ", "Aérodromes", At));
        Should.Throw<DomainRuleViolationException>(() => OptionSet.Create("aerodromes", "Aerodromes", " ", At));
    }

    [Fact]
    public void GivenSet_WhenItemsAreAdded_ThenTheyKeepOrderTheyWereAddedIn()
    {
        // Given / When
        var set = Aerodromes();

        // Then
        set.Items.Select(item => item.Code).ShouldBe(["golden", "lumby"]);
        set.Items.Select(item => item.DisplayOrder).ShouldBe([0, 1]);
    }

    [Fact]
    public void GivenCodeAlreadyInSet_WhenAddedAgain_ThenRejected()
    {
        // Given
        var set = Aerodromes();

        // When / Then
        Should.Throw<DomainRuleViolationException>(() => set.Add("golden", "Golden", "Golden"));
    }

    [Fact]
    public void GivenRemovedCode_WhenAddedAgain_ThenOriginalItemIsRevived()
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
    public void GivenSet_WhenRenamed_ThenBothLanguagesChangeAndBlankIsRefused()
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
    public void GivenLiveItem_WhenRelabelled_ThenCodeIsUnchanged()
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
    public void GivenCodeIsNotLive_WhenRelabelled_ThenRejected()
    {
        // Given
        var set = Aerodromes();
        set.Remove("lumby", At.AddHours(1));

        // When / Then
        Should.Throw<DomainRuleViolationException>(() => set.Relabel("lumby", "Lumby", "Lumby"));
        Should.Throw<DomainRuleViolationException>(() => set.Relabel("pemberton", "Pemberton", "Pemberton"));
    }

    [Fact]
    public void GivenSet_WhenArranged_ThenItemsTakeGivenOrder()
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
    public void GivenArrangementIsNotWholeList_WhenApplied_ThenRejected()
    {
        // Given
        var set = Aerodromes();

        // When / Then — a partial order would leave the rest somewhere arbitrary
        Should.Throw<DomainRuleViolationException>(() => set.Arrange(["golden"]));
        Should.Throw<DomainRuleViolationException>(() => set.Arrange(["golden", "golden"]));
        Should.Throw<DomainRuleViolationException>(() => set.Arrange(["golden", "pemberton"]));
    }

    [Fact]
    public void GivenSet_WhenItemIsRemoved_ThenLeavesLiveListWithoutBeingErased()
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
    public void GivenSet_WhenDeleted_ThenAndItemsAreRetiredAndRefusesFurtherEdits()
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
    public void GivenDeletedSet_WhenDeletedAgain_ThenFirstTimestampStands()
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
    public void GivenBlankItemLabel_WhenAdded_ThenRejected()
    {
        // Given
        var set = Aerodromes();

        // When / Then
        Should.Throw<DomainRuleViolationException>(() => set.Add("pemberton", " ", "Pemberton"));
        Should.Throw<DomainRuleViolationException>(() => set.Add("pemberton", "Pemberton", " "));
    }

    [Fact]
    public void GivenSet_WhenReadAsRevisionOptions_ThenEachCarriesSourceItem()
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
