using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Amazon.S3;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     The edges of the private-attachment endpoints (ADR-0135), against real
///     PostgreSQL and S3-compatible containers. Who may use them and what they store
///     are the REQ-MED-046..052 and REQ-MOD-107..114 acceptance scenarios; these
///     cover malformed input, reports and attachments that are not there, a cap
///     lowered between mint and claim, and a quarantine copy that cannot be erased.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public class PrivateAttachmentEndpointTests(ApiPostgresFixture fixture)
{
	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Theory]
	[InlineData("not-an-id")]
	[InlineData("AAAAAAAAAAA")]
	public async Task GivenReportThatIsNotThere_WhenMintingAddingOrListing_ThenNotFound(string reportId)
	{
		// Given
		using var officer = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);
		var root = $"/api/admin/reports/{reportId}/private-attachments";

		// When
		using var minted = await officer.PostAsJsonAsync($"{root}/uploads", new { contentType = "application/zip", byteSize = 10 });
		using var added = await officer.PostAsJsonAsync(root, new { uploadId = UploadId.New().Value, fileName = "Synthetic.zip" });
		using var listed = await officer.GetAsync(root);

		// Then
		new[] { minted.StatusCode, added.StatusCode, listed.StatusCode }.ShouldAllBe(status => status == HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenDeclarationWithoutSize_WhenMinting_ThenBadRequest()
	{
		// Given
		var root = await Root();
		using var officer = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		using var minted = await officer.PostAsJsonAsync($"{root}/uploads", new { contentType = "application/zip" });

		// Then
		minted.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("not an upload id")]
	public async Task GivenMalformedUploadId_WhenAdding_ThenBadRequest(string? uploadId)
	{
		// Given
		var root = await Root();
		using var officer = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		using var added = await officer.PostAsJsonAsync(root, new { uploadId, fileName = "Synthetic.zip" });

		// Then
		added.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Theory]
	[InlineData("not-an-id")]
	[InlineData("AAAAAAAAAAA")]
	public async Task GivenAttachmentThatIsNotThere_WhenDownloadingOrRemoving_ThenNotFound(string attachmentId)
	{
		// Given
		var root = await Root();
		using var officer = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		using var downloaded = await officer.GetAsync($"{root}/{attachmentId}/download");
		using var removed = await officer.DeleteAsync($"{root}/{attachmentId}");

		// Then
		downloaded.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		removed.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenAttachmentOnAnotherReport_WhenDownloadedThroughThisReport_ThenNotFound()
	{
		// Given
		var (_, attachmentId) = await Attached(_factory);
		var otherRoot = await Root();
		using var officer = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		using var downloaded = await officer.GetAsync($"{otherRoot}/{attachmentId}/download");

		// Then
		downloaded.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenCapLoweredAfterMint_WhenClaimed_ThenRefusedAsTooLargeAndNothingStored()
	{
		// Given
		var root = await Root();
		using var officer = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);
		var uploadId = await Send(officer, root, new byte[64]);
		using var lowered = _factory.WithWebHostBuilder(builder =>
			builder.UseSetting("HpacSafety:Media:PrivateAttachments:MaxByteSize", "16"));
		using var loweredOfficer = await SignedInClient.As(lowered, MemberRole.SafetyOfficer);

		// When
		using var added = await loweredOfficer.PostAsJsonAsync(root, new { uploadId, fileName = "Synthetic.zip" });

		// Then
		added.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await added.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reason").GetString().ShouldBe("too_large");
		(await officer.GetFromJsonAsync<JsonElement>(root)).GetArrayLength().ShouldBe(0);
	}

	[Fact]
	public async Task GivenQuarantineThatCannotBeErased_WhenClaimed_ThenClaimStillSucceeds()
	{
		// Given
		using var stubborn = _factory.WithWebHostBuilder(builder =>
			builder.ConfigureTestServices(services =>
			{
				var original = services.Last(descriptor => descriptor.ServiceType == typeof(IBlobStore));
				services.Remove(original);
				services.AddSingleton<IBlobStore>(provider =>
					new UnerasableBlobStore((IBlobStore)original.ImplementationFactory!(provider)));
			}));

		// When
		var (root, attachmentId) = await Attached(stubborn);

		// Then
		// The row committed; the lifecycle rule, not the claim, removes the upload.
		using var officer = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);
		var listed = await officer.GetFromJsonAsync<JsonElement>(root);
		listed.EnumerateArray().Select(attachment => attachment.GetProperty("id").GetString()).ShouldBe([attachmentId]);
	}

	[Theory]
	[InlineData("not-an-id")]
	[InlineData("AAAAAAAAAAA")]
	public async Task GivenAttachmentThatIsNotThere_WhenNoteRefersToIt_ThenBadRequest(string attachmentId)
	{
		// Given
		var (root, _) = await Attached(_factory);
		var notes = root.Replace("private-attachments", "private-notes", StringComparison.Ordinal);
		using var officer = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		using var added = await officer.PostAsJsonAsync(notes, new { text = "Synthetic.", attachmentId });

		// Then
		added.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await officer.GetFromJsonAsync<JsonElement>(notes)).GetArrayLength().ShouldBe(0);
	}

	[Fact]
	public async Task GivenNoteReferringToAttachment_WhenEditedToKeepIt_ThenStillRefersToIt()
	{
		// Given
		var (root, attachmentId) = await Attached(_factory);
		var notes = root.Replace("private-attachments", "private-notes", StringComparison.Ordinal);
		using var officer = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);
		using var added = await officer.PostAsJsonAsync(notes, new { text = "Synthetic.", attachmentId });
		var noteId = (await added.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

		// When
		using var edited = await officer.PutAsJsonAsync($"{notes}/{noteId}", new { text = "Synthetic, edited.", revision = 1, attachmentId });

		// Then
		edited.StatusCode.ShouldBe(HttpStatusCode.OK);
		var note = await edited.Content.ReadFromJsonAsync<JsonElement>();
		note.GetProperty("attachment").GetProperty("id").GetString().ShouldBe(attachmentId);
		note.GetProperty("attachment").GetProperty("fileName").GetString().ShouldBe("Synthetic.zip");
	}

	private async Task<string> Root()
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var report = new Report(Locale.EnCa, DateTimeOffset.UtcNow);
		database.Reports.Add(report);
		await database.SaveChangesAsync();
		return $"/api/admin/reports/{report.Id.Value}/private-attachments";
	}

	private static async Task<string> Send(HttpClient officer,
										   string root,
										   byte[] bytes)
	{
		using var minted = await officer.PostAsJsonAsync($"{root}/uploads", new { contentType = "application/zip", byteSize = bytes.Length });
		minted.StatusCode.ShouldBe(HttpStatusCode.Created);
		var body = await minted.Content.ReadFromJsonAsync<JsonElement>();
		using var put = await DirectUpload.Put(new Uri(body.GetProperty("uploadUrl").GetString()!), new ByteArrayContent(bytes), body.GetProperty("contentType").GetString()!);
		put.IsSuccessStatusCode.ShouldBeTrue();
		return body.GetProperty("uploadId").GetString()!;
	}

	private async Task<(string Root, string AttachmentId)> Attached(WebApplicationFactory<Program> host)
	{
		var root = await Root();
		using var officer = await SignedInClient.As(host, MemberRole.SafetyOfficer);
		var uploadId = await Send(officer, root, new byte[32]);
		using var added = await officer.PostAsJsonAsync(root, new { uploadId, fileName = "Synthetic.zip" });
		added.StatusCode.ShouldBe(HttpStatusCode.Created);
		return (root, (await added.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);
	}

	/// <summary>A store whose quarantine erase always fails, as a flaky storage call would.</summary>
	private sealed class UnerasableBlobStore(IBlobStore inner) : IBlobStore
	{
		public Task<Uri> CreateReadUrl(BlobKey key, string downloadFileName, TimeSpan lifetime, CancellationToken cancellationToken)
		{
			return inner.CreateReadUrl(key, downloadFileName, lifetime, cancellationToken);
		}

		public Task<Uri> CreateInlineReadUrl(BlobKey key, string contentType, TimeSpan lifetime, CancellationToken cancellationToken)
		{
			return inner.CreateInlineReadUrl(key, contentType, lifetime, cancellationToken);
		}

		public Task<Uri> CreateUploadUrl(BlobKey key, string contentType, long byteSize, TimeSpan lifetime, CancellationToken cancellationToken)
		{
			return inner.CreateUploadUrl(key, contentType, byteSize, lifetime, cancellationToken);
		}

		public Task<Stream> OpenRead(BlobKey key, CancellationToken cancellationToken)
		{
			return inner.OpenRead(key, cancellationToken);
		}

		public Task<Stream> OpenReadRange(BlobKey key, long offset, long length, CancellationToken cancellationToken)
		{
			return inner.OpenReadRange(key, offset, length, cancellationToken);
		}

		public Task Write(BlobKey key, Stream content, string contentType, CancellationToken cancellationToken)
		{
			return inner.Write(key, content, contentType, cancellationToken);
		}

		public Task<StoredBlob?> Describe(BlobKey key, CancellationToken cancellationToken)
		{
			return inner.Describe(key, cancellationToken);
		}

		public Task Copy(BlobKey source, BlobKey destination, CancellationToken cancellationToken)
		{
			return inner.Copy(source, destination, cancellationToken);
		}

		public Task Delete(BlobKey key, CancellationToken cancellationToken)
		{
			throw new AmazonS3Exception("Synthetic: storage is unavailable.");
		}
	}
}
