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
    [InlineData("video/mp4")]
    [InlineData("application/pdf")]
    [InlineData("image/svg+xml")]
    [InlineData("")]
    [InlineData(null)]
    public void Given_a_content_type_this_system_cannot_strip_When_it_is_parsed_Then_it_is_refused(string? declared)
    {
        // Given / When
        var parsed = MediaType.TryParse(declared, out _);

        // Then
        parsed.ShouldBeFalse();
        Should.Throw<DomainRuleViolationException>(() => MediaType.Parse(declared));
    }

    [Fact]
    public void Given_the_accepted_set_When_it_is_read_Then_every_member_is_an_image_this_system_can_strip()
    {
        // Given / When
        var all = MediaType.All;

        // Then
        all.ShouldContain(MediaType.Jpeg);
        all.ShouldContain(MediaType.Png);
        all.ShouldContain(MediaType.WebP);
        all.ShouldAllBe(t => t.ContentType.StartsWith("image/", StringComparison.Ordinal));
    }
}
