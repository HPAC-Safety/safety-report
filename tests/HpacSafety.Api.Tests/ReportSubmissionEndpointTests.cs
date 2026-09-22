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
///     The only reporter-facing write, against a real PostgreSQL container. Every
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
			answers = new[] { new { questionRevisionId = consentRevisionId, value = "yes" } }
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
			answers = new[] { new { questionRevisionId = consentRevisionId, value = "no" } }
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
			answers = new[] { new { questionRevisionId = revisionId, value = "hello" } }
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
				new { questionRevisionId = "not-a-real-id", value = "hello" }
			}
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
				new { questionRevisionId = consentRevisionId, value = "no" }
			}
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
				new { questionRevisionId = revisionId, value = (string?)null }
			}
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
				new { questionRevisionId = revisionId, value = narrative }
			}
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
		// Given — the default limit is 5; six garbage parts is over it
		using var reporter = await SignedIn();
		var consentRevisionId = await ConsentRevisionId();
		var content = ReportPart(new
		{
			language = "en-CA",
			answers = new[] { new { questionRevisionId = consentRevisionId, value = "yes" } }
		});

		for (var i = 0; i < 6; i++)
		{
			var part = new ByteArrayContent([1, 2, 3]);
			part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
			content.Add(part, "files", $"garbage-{i}.bin");
		}

		// When
		using var response = await reporter.PostAsync(Submit, content);
		content.Dispose();

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenAFileUploadAnswer_WhenSubmittedWithAValidImage_ThenAcceptedAndFileIsLinked()
	{
		// Given
		using var admin = await SignedIn(MemberRole.Administrator);
		var key = await CreateSyntheticQuestion(admin, type: "file_upload");
		var revisionId = await RevisionIdFor(key);
		var consentRevisionId = await ConsentRevisionId();
		using var reporter = await SignedIn();

		using var image = new MagickImage(MagickColors.SkyBlue, 8, 8) { Format = MagickFormat.Png };
		var bytes = image.ToByteArray();

		var content = ReportPart(new
		{
			language = "en-CA",
			answers = new[]
			{
				new { questionRevisionId = consentRevisionId, value = (string?)"yes", attachmentPartIndexes = (int[]?)null },
				new { questionRevisionId = revisionId, value = (string?)null, attachmentPartIndexes = (int[]?)[0] }
			}
		});

		var part = new ByteArrayContent(bytes);
		part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
		content.Add(part, "files", "photo.png");

		// When
		using var response = await reporter.PostAsync(Submit, content);
		content.Dispose();

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());

		var body = await response.Content.ReadFromJsonAsync<SubmitReportResponse>();
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var file = await database.ReportFiles.SingleAsync(f => f.ReportId == TinyId.Parse(body!.Id));

		// A successfully stripped image must be viewable by a reviewer as soon
		// as submission completes — ingestion runs synchronously, so nothing
		// else will ever record the derivative if this endpoint does not.
		file.AwaitsStripping.ShouldBeFalse();
		Should.NotThrow(() => file.ViewableKey);
	}

	[Fact]
	public async Task GivenAFileIndexNeverUploaded_WhenSubmitted_ThenRejected()
	{
		// Given
		using var admin = await SignedIn(MemberRole.Administrator);
		var key = await CreateSyntheticQuestion(admin, type: "file_upload");
		var revisionId = await RevisionIdFor(key);
		var consentRevisionId = await ConsentRevisionId();
		using var reporter = await SignedIn();
		using var content = ReportPart(new
		{
			language = "en-CA",
			answers = new[]
			{
				new { questionRevisionId = consentRevisionId, value = (string?)"yes", attachmentPartIndexes = (int[]?)null },
				new { questionRevisionId = revisionId, value = (string?)null, attachmentPartIndexes = (int[]?)[0] }
			}
		});

		// When — no files part was actually attached
		using var response = await reporter.PostAsync(Submit, content);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	private Task<HttpClient> SignedIn(MemberRole role = MemberRole.User)
	{
		return SignedInClient.As(_factory, role);
	}

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private static MultipartFormDataContent ReportPart(object dto)
	{
		var content = new MultipartFormDataContent();
		var json = JsonSerializer.Serialize(dto, JsonOptions);
		content.Add(new StringContent(json), "report");
		return content;
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

	private static async Task<string> CreateSyntheticQuestion(HttpClient admin, string type = "short_text")
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
			options = Array.Empty<object>()
		};

		using var response = await admin.PostAsJsonAsync(AdminQuestions, request);
		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

		return key;
	}
}
