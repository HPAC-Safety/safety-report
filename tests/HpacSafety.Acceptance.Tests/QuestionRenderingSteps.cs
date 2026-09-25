using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     REQ-WLD-014: the form's question wording, in both languages, is the
///     database revision an Administrator saved (ADR-0062).
/// </summary>
/// <remarks>
///     The form renders what <c>GET /api/v1/questions</c> returns, so this reads it
///     from the booted API and compares it with the rows. The one CI translation
///     writes <c>locales/fr-CA.json</c> from <c>locales/en-CA.json</c>, a catalogue
///     no question's wording lives in. Every question here is synthetic.
/// </remarks>
[Binding]
public sealed class QuestionRenderingSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private static readonly Uri AdminQuestions = new("/api/admin/questions", UriKind.Relative);
	private static readonly Uri PublicQuestions = new("/api/v1/questions", UriKind.Relative);

	private readonly string _run = Guid.NewGuid().ToString("N");
	private string? _questionId;
	private JsonElement _rendered;

	[Given(@"a question revision has English and French labels, help, and options authored by an Administrator")]
	public async Task GivenAnAuthoredBilingualQuestion()
	{
		using var admin = await BootedApi.SignedInAs(MemberRole.Administrator);
		using var response = await admin.PostAsJsonAsync(AdminQuestions, new
		{
			key = (string?)null,
			type = "single_select",
			labelEn = $"Which wing were you flying? {_run}",
			labelFr = $"Quelle aile pilotiez-vous ? {_run}",
			helpTextEn = "The wing's category.",
			helpTextFr = "La catégorie de l'aile.",
			isRequired = false,
			isPrivate = false,
			isActive = true,
			options = new[]
			{
				new { code = "paraglider", labelEn = "Paraglider", labelFr = "Parapente" },
				new { code = "hang_glider", labelEn = "Hang glider", labelFr = "Deltaplane" },
			},
		});

		response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
		_questionId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
	}

	[When(@"the form renders that question")]
	public async Task WhenTheFormRendersIt()
	{
		using var anonymous = (await BootedApi.Factory()).CreateClient();
		var form = await anonymous.GetFromJsonAsync<JsonElement>(PublicQuestions);
		_rendered = form.EnumerateArray().Single(entry => entry.GetProperty("id").GetString() == _questionId);
	}

	[Then(@"both languages come from the database revision")]
	public async Task ThenBothLanguagesComeFromTheRevision()
	{
		await using var scope = (await BootedApi.Factory()).Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var id = TinyId.Parse(_questionId);
		var question = await database.Questions
			.Include(candidate => candidate.Revisions)
			.Include(candidate => candidate.AllChoices)
			.SingleAsync(candidate => candidate.Id == id);
		var revision = question.CurrentRevision;

		_rendered.GetProperty("revisionId").GetString().ShouldBe(revision.Id.Value);
		_rendered.GetProperty("labelEn").GetString().ShouldBe(revision.LabelEn);
		_rendered.GetProperty("labelFr").GetString().ShouldBe(revision.LabelFr);
		_rendered.GetProperty("helpTextEn").GetString().ShouldBe(revision.HelpTextEn);
		_rendered.GetProperty("helpTextFr").GetString().ShouldBe(revision.HelpTextFr);

		_rendered.GetProperty("options").EnumerateArray()
			.Select(option => (option.GetProperty("labelEn").GetString(), option.GetProperty("labelFr").GetString()))
			.ShouldBe(question.Choices.Select(choice => (choice.LabelEn, choice.LabelFr)));
	}

	[Then(@"no runtime or CI auto-translation service produces question rendering")]
	public void ThenNoTranslationServiceProducesIt()
	{
		// Runtime: nothing that answers the public form can reach a translator.
		Assembly.Load("HpacSafety.Api")
			.GetTypes()
			.Where(type => type.Namespace == "HpacSafety.Api.PublicQuestions")
			.SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Static
																	  | BindingFlags.Public | BindingFlags.NonPublic))
			.ShouldNotContain(method =>
				method.GetParameters().Any(parameter => parameter.ParameterType == typeof(ITranslator)));

		// CI: its one translation writes the interface catalogue, and no
		// question's wording is in it.
		var locales = Path.Combine(RepositoryRoot(), "locales");
		foreach (var catalogue in Directory.GetFiles(locales, "*.json"))
		{
			File.ReadAllText(catalogue).ShouldNotContain(_run);
		}

		File.ReadAllText(Path.Combine(RepositoryRoot(), ".github", "workflows", "i18n-translate.yml"))
			.ShouldContain("node tools/translate-locale.mjs --generate --locales locales");
	}

	private static string RepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);

		while (directory is not null
			   && !File.Exists(Path.Combine(directory.FullName, "HpacSafety.slnx")))
		{
			directory = directory.Parent;
		}

		return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
	}
}
