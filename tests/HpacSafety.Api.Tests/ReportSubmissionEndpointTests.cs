using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Api.Reports;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Infrastructure.Persistence;
using ImageMagick;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     The reporter's report write, against real PostgreSQL and S3-compatible containers. Every
///     report here is synthetic. See issue #14 and
///     <c>features/report-submission/report-submission.feature</c>.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public class ReportSubmissionEndpointTests(ApiPostgresFixture fixture)
{
	private static readonly Uri Submit = new("/api/v1/reports", UriKind.Relative);
	private static readonly Uri PublicQuestions = new("/api/v1/questions", UriKind.Relative);
	private static readonly Uri AdminQuestions = new("/api/admin/questions", UriKind.Relative);
	private static readonly Uri AwaitingTranslation = new("/api/admin/answers/awaiting-translation", UriKind.Relative);

	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Fact]
	public async Task GivenNoBearerToken_WhenSubmitted_ThenRejectedBeforeAnyStateIsCreated()
	{
		// Given
		using var client = _factory.CreateClient();
		using var content = ReportPart(new { language = "en-CA", answers = Array.Empty<object>() });

		// When
		using var response = await client.PostAsync(Submit, content);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GivenValidConsentOnlySubmission_WhenPosted_ThenAcceptedWithAnOpaqueId()
	{
		// Given
		using var reporter = await SignedIn();
		var consentRevisionId = await ConsentRevisionId();
		using var content = ReportPart(new
		{
			language = "en-CA",
			answers = new[] { new { questionRevisionId = consentRevisionId, value = "yes" } },
		});

		// When
		using var response = await reporter.PostAsync(Submit, content);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		body.GetProperty("status").GetString().ShouldBe("submitted");
		body.GetProperty("id").GetString().ShouldNotBeNullOrWhiteSpace();
	}

	[Theory]
	[InlineData("User")]
	[InlineData("SafetyOfficer")]
	[InlineData("Administrator")]
	public async Task GivenAnyMemberRole_WhenSubmitted_ThenAccepted(string roleName)
	{
		// Given
		var role = Enum.Parse<MemberRole>(roleName);
		using var reporter = await SignedIn(role);
		var consentRevisionId = await ConsentRevisionId();
		using var content = ReportPart(new
		{
			language = "en-CA",
			answers = new[] { new { questionRevisionId = consentRevisionId, value = "no" } },
		});

		// When
		using var response = await reporter.PostAsync(Submit, content);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
	}

	[Fact]
	public async Task GivenNoAnswerToConsent_WhenSubmitted_ThenRejected()
	{
		// Given
		using var admin = await SignedIn(MemberRole.Administrator);
		var key = await CreateSyntheticQuestion(admin);
		var revisionId = await RevisionIdFor(key);
		using var reporter = await SignedIn();
		using var content = ReportPart(new
		{
			language = "en-CA",
			answers = new[] { new { questionRevisionId = revisionId, value = "hello" } },
		});

		// When
		using var response = await reporter.PostAsync(Submit, content);

		// Then — consent is the only required answer, and it is missing entirely
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenUnknownRevisionId_WhenSubmitted_ThenRejected()
	{
		// Given
		using var reporter = await SignedIn();
		var consentRevisionId = await ConsentRevisionId();
		using var content = ReportPart(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = consentRevisionId, value = "yes" },
				new { questionRevisionId = "not-a-real-id", value = "hello" },
			},
		});

		// When
		using var response = await reporter.PostAsync(Submit, content);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenDuplicateRevisionId_WhenSubmitted_ThenRejected()
	{
		// Given
		using var reporter = await SignedIn();
		var consentRevisionId = await ConsentRevisionId();
		using var content = ReportPart(new
		{
			language = "en-CA",
			answers = new[]
			{
				new { questionRevisionId = consentRevisionId, value = "yes" },
				new { questionRevisionId = consentRevisionId, value = "no" },
			},
		});

		// When
		using var response = await reporter.PostAsync(Submit, content);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenSkippedOptionalQuestion_WhenSubmitted_ThenAccepted()
	{
		// Given
		using var admin = await SignedIn(MemberRole.Administrator);
		var key = await CreateSyntheticQuestion(admin);
		var revisionId = await RevisionIdFor(key);
		var consentRevisionId = await ConsentRevisionId();
		using var reporter = await SignedIn();
		using var content = ReportPart(new
		{
			language = "en-CA",
			answers = new[]
			{
				new { questionRevisionId = consentRevisionId, value = (string?)"yes" },
				new { questionRevisionId = revisionId, value = (string?)null },
			},
		});

		// When
		using var response = await reporter.PostAsync(Submit, content);

		// Then — every ordinary question may be skipped
		response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
	}

	[Fact]
	public async Task GivenNarrativeAnswer_WhenSubmitted_ThenValueIsImmutableAndAwaitsTranslation()
	{
		// Given — a long-text answer is not a select, but ADR-0080 still queues
		// it for translation, and nothing on this path ever translates it
		using var admin = await SignedIn(MemberRole.Administrator);
		var key = await CreateSyntheticQuestion(admin, type: "long_text");
		var revisionId = await RevisionIdFor(key);
		var consentRevisionId = await ConsentRevisionId();
		using var reporter = await SignedIn();
		const string narrative = "Wind picked up on final approach.";
		using var content = ReportPart(new
		{
			language = "en-CA",
			answers = new[]
			{
				new { questionRevisionId = consentRevisionId, value = "yes" },
				new { questionRevisionId = revisionId, value = narrative },
			},
		});

		// When
		using var response = await reporter.PostAsync(Submit, content);
		response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

		// Then — the answer shows up in the translation queue, untranslated,
		// with the exact submitted words
		var queue = await admin.GetFromJsonAsync<JsonElement>(AwaitingTranslation);
		var entries = queue.GetProperty("answers").EnumerateArray().ToList();
		entries.ShouldContain(entry => entry.GetProperty("value").GetString() == narrative);
	}

	[Fact]
	public async Task GivenAttachmentCountOverLimit_WhenSubmitted_ThenRejected()
	{
		// Given — the default limit is 5; six named uploads is over it
		using var admin = await SignedIn(MemberRole.Administrator);
		var revisionId = await RevisionIdFor(await CreateSyntheticQuestion(admin, type: "file_upload"));
		var consentRevisionId = await ConsentRevisionId();
		using var reporter = await SignedIn();
		var attachments = Enumerable.Range(0, 6)
			.Select(i => new { uploadId = UploadId.New().Value, fileName = $"photo-{i}.png" })
			.ToArray();
		using var content = ReportPart(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = consentRevisionId, value = "yes" },
				new { questionRevisionId = revisionId, attachments },
			},
		});

		// When
		using var response = await reporter.PostAsync(Submit, content);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenAFileUploadAnswer_WhenSubmittedWithAnUploadedImage_ThenAcceptedAndFileIsLinkedUnderItsName()
	{
		// Given
		using var admin = await SignedIn(MemberRole.Administrator);
		var revisionId = await RevisionIdFor(await CreateSyntheticQuestion(admin, type: "file_upload"));
		var consentRevisionId = await ConsentRevisionId();
		using var reporter = await SignedIn();
		var uploadId = await UploadPng(reporter);

		using var content = ReportPart(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = consentRevisionId, value = "yes" },
				new
				{
					questionRevisionId = revisionId,
					attachments = new[] { new { uploadId, fileName = "C:\\photos\\Launch \"site\".png" } },
				},
			},
		});

		// When
		using var response = await reporter.PostAsync(Submit, content);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());

		var body = await response.Content.ReadFromJsonAsync<SubmitReportResponse>();
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var file = await database.ReportFiles.SingleAsync(f => f.ReportId == TinyId.Parse(body!.Id));

		// Submission copies the original and decodes nothing; the Worker writes
		// the derivative from the file's outbox message (ADR-0098).
		file.AwaitsStripping.ShouldBeTrue();
		file.ContentType.ShouldBe("image/png");
		var outbox = await database.OutboxMessages
			.Where(message => message.Payload == file.Id.Value)
			.Select(message => message.Type)
			.ToListAsync();
		outbox.ShouldBe([Core.Features.Outbox.OutboxMessageType.ProcessAttachment]);

		// The reporter's name is kept, sanitized; the original is named by the
		// file's own id and never by the name or the upload id (ADR-0097).
		file.OriginalFileName.ShouldBe("Launch site.png");
		file.BlobKey.ShouldBe($"{body!.Id}/original/{file.Id}");
		(await ObjectExists(file.BlobKey)).ShouldBeTrue();
		(await ObjectExists($"{body.Id}/stripped/{file.Id}")).ShouldBeFalse();

		// The claimed upload has left quarantine (REQ-SUB-042).
		(await ObjectExists($"quarantine/{uploadId}")).ShouldBeFalse();
	}

	[Fact]
	public async Task GivenAnUploadThatNoLongerExists_WhenSubmitted_ThenRejectedNamingItAndNothingIsStored()
	{
		// Given
		using var admin = await SignedIn(MemberRole.Administrator);
		var revisionId = await RevisionIdFor(await CreateSyntheticQuestion(admin, type: "file_upload"));
		var consentRevisionId = await ConsentRevisionId();
		using var reporter = await SignedIn();
		var live = await UploadPng(reporter);
		var expired = UploadId.New().Value;
		var reportsBefore = await ReportCount();

		using var content = ReportPart(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = consentRevisionId, value = "yes" },
				new
				{
					questionRevisionId = revisionId,
					attachments = new[] { new { uploadId = live, fileName = "a.png" }, new { uploadId = expired, fileName = "b.png" } },
				},
			},
		});

		// When
		using var response = await reporter.PostAsync(Submit, content);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
		problem.GetProperty("expiredUploadIds").EnumerateArray().Select(id => id.GetString()).ShouldBe([expired]);
		(await ReportCount()).ShouldBe(reportsBefore);

		// The live upload is untouched and can still be claimed.
		(await ObjectExists($"quarantine/{live}")).ShouldBeTrue();
	}

	[Theory]
	[InlineData("not-an-upload-id")]
	[InlineData("")]
	public async Task GivenAMalformedUploadId_WhenSubmitted_ThenRejected(string uploadId)
	{
		// Given
		using var admin = await SignedIn(MemberRole.Administrator);
		var revisionId = await RevisionIdFor(await CreateSyntheticQuestion(admin, type: "file_upload"));
		var consentRevisionId = await ConsentRevisionId();
		using var reporter = await SignedIn();
		using var content = ReportPart(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = consentRevisionId, value = "yes" },
				new { questionRevisionId = revisionId, attachments = new[] { new { uploadId, fileName = "a.png" } } },
			},
		});

		// When
		using var response = await reporter.PostAsync(Submit, content);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenTheSameUploadNamedTwice_WhenSubmitted_ThenRejected()
	{
		// Given
		using var admin = await SignedIn(MemberRole.Administrator);
		var revisionId = await RevisionIdFor(await CreateSyntheticQuestion(admin, type: "file_upload"));
		var consentRevisionId = await ConsentRevisionId();
		using var reporter = await SignedIn();
		var uploadId = await UploadPng(reporter);
		using var content = ReportPart(new
		{
			language = "en-CA",
			answers = new object[]
			{
				new { questionRevisionId = consentRevisionId, value = "yes" },
				new
				{
					questionRevisionId = revisionId,
					attachments = new[] { new { uploadId, fileName = "a.png" }, new { uploadId, fileName = "b.png" } },
				},
			},
		});

		// When
		using var response = await reporter.PostAsync(Submit, content);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenAMultipartBody_WhenSubmitted_ThenRejected()
	{
		// Given — the old multipart shape is gone (ADR-0096)
		using var reporter = await SignedIn();
		using var content = new MultipartFormDataContent { { new StringContent("{}"), "report" } };

		// When
		using var response = await reporter.PostAsync(Submit, content);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	private static async Task<string> UploadPng(HttpClient reporter)
	{
		using var image = new MagickImage(MagickColors.SkyBlue, 8, 8) { Format = MagickFormat.Png };
		using var body = new ByteArrayContent(image.ToByteArray());
		body.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");

		using var response = await reporter.PostAsync(new Uri("/api/v1/uploads", UriKind.Relative), body);
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

		var upload = await response.Content.ReadFromJsonAsync<JsonElement>();
		return upload.GetProperty("uploadId").GetString()!;
	}

	private async Task<bool> ObjectExists(string key)
	{
		try
		{
			await fixture.Storage.GetObjectMetadataAsync(ApiPostgresFixture.BucketName, key);
			return true;
		}
		catch (Amazon.S3.AmazonS3Exception missing) when (missing.StatusCode == HttpStatusCode.NotFound)
		{
			return false;
		}
	}

	private async Task<int> ReportCount()
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		return await database.Reports.IgnoreQueryFilters().CountAsync();
	}

	private Task<HttpClient> SignedIn(MemberRole role = MemberRole.User)
	{
		return SignedInClient.As(_factory, role);
	}

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private static StringContent ReportPart(object dto)
	{
		return new StringContent(JsonSerializer.Serialize(dto, JsonOptions), System.Text.Encoding.UTF8, "application/json");
	}

	private async Task<string> ConsentRevisionId()
	{
		await EnsureConsentQuestionExists();
		return await RevisionIdFor(QuestionKey.ConsentPublish);
	}

	/// <summary>
	///     No deployed environment nor this repository's admin question-authoring
	///     endpoint currently creates the <c>consent_publish</c> system question —
	///     it is only ever constructed directly in domain-level tests. That is a
	///     real, pre-existing gap the reporter-facing form will eventually need
	///     closed by its own change; it is not this endpoint's to fix. Here, tests
	///     seed it directly against the same database the booted API is using, the
	///     same way <c>OutboxAtomicityTests</c> does — idempotently, since this
	///     collection shares one database across every test in it.
	/// </summary>
	private async Task EnsureConsentQuestionExists()
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		if (await database.Questions.AnyAsync(question => question.Key == QuestionKey.ConsentPublish))
		{
			return;
		}

		database.Questions.Add(Question.CreateConsentPublish(
			"May we publish a de-identified version of your report?",
			"Pouvons-nous publier une version anonymisée de votre rapport ?",
			DateTimeOffset.UtcNow));

		await database.SaveChangesAsync();
	}

	private async Task<string> RevisionIdFor(string key)
	{
		using var client = _factory.CreateClient();
		var questions = await FlattenedPublicQuestions(client);
		var question = questions.Single(candidate => candidate.GetProperty("key").GetString() == key);
		return question.GetProperty("revisionId").GetString()!;
	}

	private static async Task<List<JsonElement>> FlattenedPublicQuestions(HttpClient client)
	{
		var body = await client.GetFromJsonAsync<JsonElement>(PublicQuestions);
		var flattened = new List<JsonElement>();

		void Flatten(JsonElement question)
		{
			flattened.Add(question);
			foreach (var child in question.GetProperty("children").EnumerateArray())
			{
				Flatten(child);
			}
		}

		foreach (var question in body.EnumerateArray())
		{
			Flatten(question);
		}

		return flattened;
	}

	private static async Task<string> CreateSyntheticQuestion(HttpClient admin,
															  string type = "short_text")
	{
		var key = $"synthetic_{Guid.NewGuid():N}"[..40];
		var request = new
		{
			key,
			type,
			labelEn = "A synthetic question",
			labelFr = "Une question synthétique",
			helpTextEn = (string?)null,
			helpTextFr = (string?)null,
			placeholderEn = (string?)null,
			placeholderFr = (string?)null,
			isRequired = false,
			isPrivate = false,
			isActive = true,
			dependsOnQuestionId = (string?)null,
			dependsOnOptionCode = (string?)null,
			optionSetId = (string?)null,
			groupedUnderQuestionId = (string?)null,
			allowsReporterAdditions = false,
			options = Array.Empty<object>(),
		};

		using var response = await admin.PostAsJsonAsync(AdminQuestions, request);
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

		return key;
	}
}
