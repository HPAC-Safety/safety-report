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
///     The media-consent answer as a real submission records it (REQ-QB-114 to
///     REQ-QB-117, ADR-0117, ADR-0119): an image or document uploaded through
///     <c>/api/v1/uploads</c>, publication consent yes, and the seeded
///     <c>consent_media</c> system question answered — or not — through the
///     booted API, against its current wording or a superseded one.
/// </summary>
[Binding]
[Scope(Feature = "Question bank and form")]
public sealed class MediaConsentSteps
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private HttpClient? _reporter;
	private string? _uploadId;
	private string? _mediaAnswer;
	private string? _mediaRevisionId;
	private string _fileName = "launch-site.png";
	private HttpResponseMessage? _response;
	private Question? _seeded;
	private string? _reportId;

	[Given(@"a submission answers yes to publication consent and attaches an image")]
	public async Task GivenASubmissionWithAnImage()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		_uploadId = await UploadImage(_reporter);
	}

	[Given(@"a submission answers yes to publication consent and attaches a document")]
	public async Task GivenASubmissionWithADocument()
	{
		_reporter = await BootedApi.SignedInAs(MemberRole.User);
		_uploadId = await UploadDocument(_reporter);
		_fileName = "checklist.pdf";
	}

	[Given(@"^it answers yes to the consent_media question's (current|earlier, superseded) revision$")]
	public async Task GivenItAnswersYesToARevision(string revision)
	{
		_mediaAnswer = "yes";
		var question = await MediaConsentQuestion();
		_mediaRevisionId = revision == "current"
			? question.CurrentRevision.Id.Value
			: question.Revisions
				.Where(candidate => candidate.RevisionNumber < question.CurrentRevision.RevisionNumber)
				.MaxBy(candidate => candidate.RevisionNumber)!.Id.Value;
	}

	[Given(@"the consent_media question as seeded")]
	public async Task GivenTheSeededMediaConsentQuestion()
	{
		_seeded = await MediaConsentQuestion();
	}

	[Then(@"its wording in both languages asks about photos, videos, and documents")]
	public void ThenItsWordingNamesEveryKindOfFile()
	{
		var revision = _seeded!.CurrentRevision;
		foreach (var word in new[] { "photos", "videos", "documents" })
		{
			revision.HelpTextEn!.ShouldContain(word);
		}

		foreach (var word in new[] { "photos", "vidéos", "documents" })
		{
			revision.HelpTextFr!.ShouldContain(word);
		}
	}

	[Then(@"it says that documents are published exactly as they were uploaded and may contain personal details")]
	public void ThenItSaysDocumentsArePublishedAsUploaded()
	{
		var revision = _seeded!.CurrentRevision;
		revision.HelpTextEn!.ShouldContain("Documents are published exactly as you uploaded them");
		revision.HelpTextEn!.ShouldContain("personal details");
		revision.HelpTextFr!.ShouldContain("Les documents sont publiés exactement tels que vous les avez téléversés");
		revision.HelpTextFr!.ShouldContain("renseignements personnels");
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
				attachments = new[] { new { uploadId = _uploadId!, fileName = _fileName } },
			},
		};

		if (_mediaAnswer is not null)
		{
			answers.Add(new { questionRevisionId = _mediaRevisionId ?? await MediaConsentRevisionId(), value = (string?)_mediaAnswer });
		}

		using var content = new StringContent(
			JsonSerializer.Serialize(new { language = "en-CA", answers }, JsonOptions), System.Text.Encoding.UTF8, "application/json");
		_response = await _reporter!.PostAsync(new Uri("/api/v1/reports", UriKind.Relative), content);
	}

	[Then(@"the report records media consent as {word}")]
	public async Task ThenTheReportRecordsMediaConsent(string recorded)
	{
		(await SubmittedReport()).ConsentMedia.ShouldBe(Consent(recorded));
	}

	[Then(@"the report records document consent as {word}")]
	public async Task ThenTheReportRecordsDocumentConsent(string recorded)
	{
		(await SubmittedReport()).ConsentDocuments.ShouldBe(Consent(recorded));
	}

	private async Task<Core.Features.Reporting.Report> SubmittedReport()
	{
		if (_reportId is null)
		{
			var body = await _response!.Content.ReadAsStringAsync();
			_response.StatusCode.ShouldBe(HttpStatusCode.Accepted, body);
			using var document = JsonDocument.Parse(body);
			_reportId = document.RootElement.GetProperty("id").GetString()!;
		}

		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		return await database.Reports.AsNoTracking().SingleAsync(candidate => candidate.Id == TinyId.Parse(_reportId));
	}

	private static bool? Consent(string recorded)
	{
		return recorded switch
		{
			"yes" => true,
			"no" => false,
			_ => null,
		};
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

	private static async Task<string> UploadDocument(HttpClient reporter)
	{
		using var body = new ByteArrayContent("%PDF-1.7\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n"u8.ToArray());
		body.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

		using var response = await reporter.PostAsync(new Uri("/api/v1/uploads", UriKind.Relative), body);
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("uploadId").GetString()!;
	}

	private static async Task<string> MediaConsentRevisionId()
	{
		return (await MediaConsentQuestion()).CurrentRevision.Id.Value;
	}

	private static async Task<Question> MediaConsentQuestion()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		// Seeded by the ShowPublishedMedia migration and reworded to name
		// documents by OfferPublishedDocuments, on every database.
		var question = await database.Questions
			.AsNoTracking()
			.Include(candidate => candidate.Revisions)
			.SingleAsync(candidate => candidate.Key == QuestionKey.ConsentMedia);

		question.IsSystem.ShouldBeTrue();
		return question;
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
