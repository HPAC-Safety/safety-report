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
        var upload = await slot.CreateAsync(ReportId, "photo.jpg", MediaType.Jpeg, TimeSpan.FromMinutes(5), CancellationToken.None);

        // Then
        // A URL that wrote straight into a report's Restricted record would put
        // unverified bytes where verified ones live. It never names the
        // compartment, so it cannot.
        upload.Key.Compartment.ShouldBe(MediaCompartment.Quarantine);
        upload.Key.Value.ShouldBe("quarantine/dQw4w9WgXcQ/photo.jpg");
        upload.Url.ShouldNotBeNull();
    }

    [Fact]
    public async Task Given_a_lifetime_beyond_the_cap_When_an_upload_slot_is_issued_Then_it_is_refused()
    {
        // Given
        var slot = new MediaUploadSlot(new InMemoryBlobStore());

        // When / Then
        await Should.ThrowAsync<DomainRuleViolationException>(
            () => slot.CreateAsync(ReportId, "photo.jpg", MediaType.Jpeg, BlobUrlLifetime.Maximum + TimeSpan.FromMinutes(1), CancellationToken.None));
    }

    [Fact]
    public async Task Given_a_report_id_that_is_not_a_tiny_id_When_an_upload_slot_is_issued_Then_it_is_refused()
    {
        // Given
        var slot = new MediaUploadSlot(new InMemoryBlobStore());

        // When / Then
        await Should.ThrowAsync<DomainRuleViolationException>(
            () => slot.CreateAsync("not-an-id", "photo.jpg", MediaType.Jpeg, TimeSpan.FromMinutes(5), CancellationToken.None));
    }
}
