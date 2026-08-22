using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using Shouldly;

namespace HpacSafety.Core.Tests.Media;

public class MediaUploadSlotTests
{
    private const string ReportId = "dQw4w9WgXcQ";

    [Fact]
    public async Task Given_a_report_When_an_upload_slot_is_issued_Then_the_url_writes_only_into_quarantine()
    {
        // Given
        var slot = new MediaUploadSlot(new InMemoryBlobStore());

        // When
        var upload = await slot.CreateAsync(ReportId, MediaType.Jpeg, TimeSpan.FromMinutes(5), CancellationToken.None);

        // Then
        // A URL that wrote straight into a report's Restricted record would put
        // unverified bytes where verified ones live. It never names the
        // compartment, so it cannot.
        upload.Key.Compartment.ShouldBe(MediaCompartment.Quarantine);
        upload.Key.ReportId.ShouldBe(ReportId);
        upload.Key.Value.ShouldStartWith("quarantine/dQw4w9WgXcQ/");
        upload.Url.ShouldNotBeNull();
    }

    [Fact]
    public async Task Given_two_upload_slots_When_they_are_issued_Then_the_file_name_is_minted_rather_than_carried_from_the_client()
    {
        // Given
        var slot = new MediaUploadSlot(new InMemoryBlobStore());

        // When
        var first = await slot.CreateAsync(ReportId, MediaType.Jpeg, TimeSpan.FromMinutes(5), CancellationToken.None);
        var second = await slot.CreateAsync(ReportId, MediaType.Jpeg, TimeSpan.FromMinutes(5), CancellationToken.None);

        // Then
        // A camera roll name is Restricted data in its own right —
        // "mt-7-tandem-dave.jpg" names a site and a person — and a key reaches
        // bucket access logs and every pre-signed URL. There is no parameter to
        // pass one through.
        first.Key.FileName.ShouldNotBe(second.Key.FileName);
        first.Key.FileName.ShouldEndWith(".jpg");
        first.Key.FileName.Length.ShouldBe(11 + ".jpg".Length);
    }

    [Fact]
    public async Task Given_a_video_slot_When_it_is_issued_Then_the_minted_name_carries_the_declared_extension()
    {
        // Given
        var slot = new MediaUploadSlot(new InMemoryBlobStore());

        // When
        var upload = await slot.CreateAsync(ReportId, MediaType.Mp4, TimeSpan.FromMinutes(5), CancellationToken.None);

        // Then
        upload.Key.FileName.ShouldEndWith(".mp4");
    }

    [Fact]
    public async Task Given_a_lifetime_beyond_the_cap_When_an_upload_slot_is_issued_Then_it_is_refused()
    {
        // Given
        var slot = new MediaUploadSlot(new InMemoryBlobStore());

        // When / Then
        await Should.ThrowAsync<DomainRuleViolationException>(
            () => slot.CreateAsync(ReportId, MediaType.Jpeg, BlobUrlLifetime.Maximum + TimeSpan.FromMinutes(1), CancellationToken.None));
    }

    [Fact]
    public async Task Given_a_report_id_that_is_not_a_tiny_id_When_an_upload_slot_is_issued_Then_it_is_refused()
    {
        // Given
        var slot = new MediaUploadSlot(new InMemoryBlobStore());

        // When / Then
        await Should.ThrowAsync<DomainRuleViolationException>(
            () => slot.CreateAsync("not-an-id", MediaType.Jpeg, TimeSpan.FromMinutes(5), CancellationToken.None));
    }
}
