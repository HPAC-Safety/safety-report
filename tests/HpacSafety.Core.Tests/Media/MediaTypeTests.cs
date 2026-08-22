using HpacSafety.Core.Features.Reporting;
using HpacSafety.Core.SharedKernel;
using Shouldly;

namespace HpacSafety.Core.Tests.Media;

public class MediaTypeTests
{
    [Fact]
    public void Given_an_accepted_content_type_When_it_is_parsed_Then_it_is_recognised()
    {
        // Given
        const string declared = "image/jpeg";

        // When
        var parsed = MediaType.TryParse(declared, out var type);

        // Then
        parsed.ShouldBeTrue();
        type.ShouldBe(MediaType.Jpeg);
        type.Extension.ShouldBe("jpg");
    }

    [Fact]
    public void Given_a_content_type_with_parameters_and_casing_When_it_is_parsed_Then_it_is_recognised()
    {
        // Given
        const string declared = "IMAGE/JPEG; charset=binary";

        // When
        var parsed = MediaType.TryParse(declared, out var type);

        // Then
        parsed.ShouldBeTrue();
        type.ShouldBe(MediaType.Jpeg);
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("image/svg+xml")]
    [InlineData("image/gif")]
    [InlineData("video/x-matroska")]
    [InlineData("")]
    [InlineData(null)]
    public void Given_a_content_type_this_system_does_not_accept_When_it_is_parsed_Then_it_is_refused(string? declared)
    {
        // Given / When
        var parsed = MediaType.TryParse(declared, out _);

        // Then
        parsed.ShouldBeFalse();
        Should.Throw<DomainRuleViolationException>(() => MediaType.Parse(declared));
    }

    [Theory]
    [InlineData("image/heic")]
    [InlineData("video/mp4")]
    [InlineData("video/quicktime")]
    public void Given_a_format_a_phone_produces_by_default_When_it_is_parsed_Then_it_is_accepted(string declared)
    {
        // Given / When
        var parsed = MediaType.TryParse(declared, out _);

        // Then
        parsed.ShouldBeTrue();
    }

    [Fact]
    public void Given_a_heic_photo_When_its_stripped_form_is_read_Then_it_is_a_jpeg()
    {
        // Given / When
        var strippedForm = MediaType.Heic.StrippedForm;

        // Then
        // The runtime imaging library decodes HEIC but cannot encode it, and a
        // reviewer needs something every browser renders. See ADR-0025.
        strippedForm.ShouldBe(MediaType.Jpeg);
        MediaType.Heic.CanBeStripped.ShouldBeTrue();
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    public void Given_an_ordinary_image_When_its_stripped_form_is_read_Then_it_keeps_its_own_format(string declared)
    {
        // Given
        var type = MediaType.Parse(declared);

        // When
        var strippedForm = type.StrippedForm;

        // Then
        strippedForm.ShouldBe(type);
    }

    [Theory]
    [InlineData("video/mp4")]
    [InlineData("video/quicktime")]
    public void Given_a_video_When_its_stripped_form_is_read_Then_there_is_none(string declared)
    {
        // Given
        var type = MediaType.Parse(declared);

        // When
        var strippedForm = type.StrippedForm;

        // Then
        // Accepted and retained, but nothing can strip it yet — see #65. A
        // reviewer sees nothing for it rather than something unsafe.
        strippedForm.ShouldBeNull();
        type.CanBeStripped.ShouldBeFalse();
        type.Kind.ShouldBe(MediaKind.Video);
    }

    [Fact]
    public void Given_the_strippable_set_When_it_is_read_Then_it_is_every_accepted_type_that_has_a_stripped_form()
    {
        // Given / When
        var strippable = MediaType.Strippable;

        // Then
        strippable.ShouldBe(MediaType.All.Where(t => t.CanBeStripped).ToArray(), ignoreOrder: true);
        strippable.ShouldAllBe(t => t.Kind == MediaKind.Image);
    }

    [Fact]
    public void Given_the_accepted_set_When_it_is_read_Then_every_member_is_an_image_or_a_video()
    {
        // Given / When
        var all = MediaType.All;

        // Then
        all.ShouldContain(MediaType.Jpeg);
        all.ShouldContain(MediaType.Heic);
        all.ShouldContain(MediaType.Mp4);
        all.ShouldAllBe(t => t.ContentType.StartsWith("image/", StringComparison.Ordinal)
            || t.ContentType.StartsWith("video/", StringComparison.Ordinal));
    }
}
