using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Infrastructure.Persistence;
using ImageMagick;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The media-consent answer as a real submission records it (REQ-QB-114,
///     REQ-QB-115, ADR-0117): an image uploaded through <c>/api/v1/uploads</c>,
///     publication consent yes, and the seeded <c>consent_media</c> system
///     question answered — or not — through the booted API.
/// </summary>
[Binding]
[Scope(Feature = "Question bank and form")]
public sealed class MediaConsentSteps
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private HttpClient? _reporter;
	private string? _uploadId;
	private string? _mediaAnswer;
	private HttpResponseMessage? _response;

	[Given(@"a submission answers yes to publication consent and attaches an image")]
	public async Task GivenASubmissionWithAnImage()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		_uploadId = await UploadImage(_reporter);
	}

	[Given(@"it answers the consent_media question with {word}")]
	public void GivenItAnswersMediaConsent(string answer)
	{
		_mediaAnswer = answer;
	}

	[Given(@"it answers the consent_media question with no answer")]
	public void GivenItLeavesMediaConsentUnanswered()
	{
		_mediaAnswer = null;
	}

	[Given(@"a submission answers the consent_media question with a value that is neither yes nor no")]
	public async Task GivenAnUnreadableMediaConsent()
	{
		await GivenASubmissionWithAnImage();
		_mediaAnswer = "maybe";
	}

	[When(@"the API accepts the submission")]
	[When(@"the reporter submits it")]
	public async Task WhenTheReporterSubmits()
	{
		var answers = new List<object>
		{
			new { questionRevisionId = await ReportSubmissionEndpointSteps.ConsentRevisionId(), value = (string?)"yes" },
			new
			{
				questionRevisionId = await FileUploadRevisionId(),
				attachments = new[] { new { uploadId = _uploadId!, fileName = "launch-site.png" } },
			},
		};

		if (_mediaAnswer is not null)
		{
			answers.Add(new { questionRevisionId = await MediaConsentRevisionId(), value = (string?)_mediaAnswer });
		}

		using var content = new StringContent(
			JsonSerializer.Serialize(new { language = "en-CA", answers }, JsonOptions), System.Text.Encoding.UTF8, "application/json");
		_response = await _reporter!.PostAsync(new Uri("/api/v1/reports", UriKind.Relative), content);
	}

	[Then(@"the report records media consent as {word}")]
	public async Task ThenTheReportRecordsMediaConsent(string recorded)
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.Accepted, await _response.Content.ReadAsStringAsync());
		var id = (await _response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var report = await database.Reports.SingleAsync(candidate => candidate.Id == TinyId.Parse(id));

		report.ConsentMedia.ShouldBe(recorded switch
		{
			"yes" => true,
			"no" => false,
			_ => (bool?)null,
		});
	}

	[Then(@"the API rejects the submission")]
	public void ThenTheApiRejectsTheSubmission()
	{
		_response!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	private static async Task<string> UploadImage(HttpClient reporter)
	{
		using var image = new MagickImage(MagickColors.SkyBlue, 8, 8) { Format = MagickFormat.Png };
		using var body = new ByteArrayContent(image.ToByteArray());
		body.Headers.ContentType = new MediaTypeHeaderValue("image/png");

		using var response = await reporter.PostAsync(new Uri("/api/v1/uploads", UriKind.Relative), body);
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("uploadId").GetString()!;
	}

	private static async Task<string> MediaConsentRevisionId()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		// Seeded by the ShowPublishedMedia migration, on every database.
		var question = await database.Questions
			.Include(candidate => candidate.Revisions)
			.SingleAsync(candidate => candidate.Key == QuestionKey.ConsentMedia);

		question.IsSystem.ShouldBeTrue();
		return question.CurrentRevision.Id.Value;
	}

	private static async Task<string> FileUploadRevisionId()
	{
		using var admin = await BootedApi.SignedInAs(MemberRole.Administrator);
		var key = $"synthetic_{Guid.NewGuid():N}"[..40];
		using var response = await admin.PostAsJsonAsync(new Uri("/api/admin/questions", UriKind.Relative), new
		{
			key,
			type = "file_upload",
			labelEn = "Attach a photo",
			labelFr = "Joindre une photo",
			isRequired = false,
			isPrivate = false,
			isActive = true,
			options = Array.Empty<object>(),
		});
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revisionId").GetString()!;
	}
}
