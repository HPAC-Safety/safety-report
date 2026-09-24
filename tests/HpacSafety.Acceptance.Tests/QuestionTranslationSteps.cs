using System.Reflection;
using HpacSafety.Core;
using HpacSafety.Infrastructure.Translation;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The non-<c>@ui</c> translation scenarios in
///     <c>features/question-bank-and-form/question-bank-and-form.feature</c>.
/// </summary>
/// <remarks>
///     <para>
///         These assert the shape of the contract rather than a provider's output:
///         that translation is a port with one purpose, that it is reached through the
///         application's own API, and that an unconfigured server says so without
///         leaking anything. The DeepL adapter's own behaviour is covered by
///         <c>HpacSafety.Infrastructure.Tests</c>, and the endpoint's by
///         <c>HpacSafety.Api.Tests</c>.
///     </para>
///     <para>
///         No credential and no network are involved, and no text here is a reporter's.
///     </para>
/// </remarks>
[Binding]
public sealed class QuestionTranslationSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private readonly StubTranslator _translator = new();
	private IReadOnlyList<string> _translated = [];
	private bool _available;

	[Given(@"an Administrator is authoring a question in one official language")]
	public void GivenAQuestionInOneLanguage()
	{
		_translator.Configured = true;
	}

	[Given(@"no translation provider is configured, in development or anywhere else")]
	public void GivenNoProvider()
	{
		_translator.Configured = false;
	}

	[When(@"they ask for the other language to be translated")]
	public async Task WhenTheOtherLanguageIsAskedFor()
	{
		_translated = await _translator.Translate(
			["Were you injured?"], Locale.EnCa, Locale.FrCa, CancellationToken.None);
	}

	[When(@"the authoring screen asks whether translation is available")]
	public void WhenAvailabilityIsAsked()
	{
		_available = _translator.IsConfigured;
	}

	[Then(@"the request goes to the application's own API rather than to a provider from the browser")]
	public void ThenTheRequestGoesToOurOwnApi()
	{
		// The browser calls POST /api/admin/translate; nothing in the web
		// application names a provider or carries a credential. A credential
		// shipped to a page is a credential published — ADR-0062.
		var endpoints = Assembly.Load("HpacSafety.Api").GetType("HpacSafety.Api.Admin.TranslationEndpoints");
		endpoints.ShouldNotBeNull();

		var webSources = Directory.GetFiles(WebSourceRoot(), "*.ts*", SearchOption.AllDirectories)
			.Concat(Directory.GetFiles(WebSourceRoot(), "*.tsx", SearchOption.AllDirectories))
			.Distinct();

		foreach (var file in webSources)
		{
			var source = File.ReadAllText(file);

			source.ShouldNotContain("deepl", Case.Insensitive, $"{file} must not name a translation provider.");
			source.ShouldNotContain("DEEPL_API_KEY", Case.Insensitive, $"{file} must not carry a credential.");
		}
	}

	[Then(@"the translated text is returned as a draft that is not saved anywhere")]
	public void ThenItIsADraft()
	{
		// The port returns text and nothing else — it has no access to the
		// question bank, and the endpoint writes nothing.
		_translated.ShouldNotBeEmpty();

		typeof(ITranslator).GetMethods()
			.ShouldAllBe(method => method.ReturnType != typeof(void));

		typeof(ITranslator).GetMethods()
			.Select(method => method.Name)
			.ShouldNotContain(name => name.Contains("Save", StringComparison.Ordinal));
	}

	[Then(@"the same action is available for the second language of a select answer awaiting translation")]
	public void ThenTheActionIsAvailableForAnAnswer()
	{
		// The same route and the same policy. What an administrator may draft
		// now includes an answer's second language (ADR-0072); what fills it is
		// still their deliberate save.
		Assembly.Load("HpacSafety.Api")
			.GetType("HpacSafety.Api.Admin.AnswerTranslationEndpoints")
			.ShouldNotBeNull()
			.GetMethod("MapAdminAnswerTranslation", BindingFlags.Static | BindingFlags.Public)
			.ShouldNotBeNull();
	}

	[Then(@"no narrative, free-text answer, or summary is ever translated this way")]
	public void ThenNoReportContentIsTranslated()
	{
		// One caller, and it is the authoring endpoint. If a second appears,
		// this fails and the decision gets revisited deliberately.
		var callers = Assembly.Load("HpacSafety.Api")
			.GetTypes()
			.Where(type => type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
				.Any(method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(ITranslator))))
			.Select(type => type.Name)
			.ToList();

		callers.ShouldBe(["TranslationEndpoints"]);
	}

	[Then(@"nothing is translated unless an Administrator asked for it")]
	public void ThenNothingTranslatesOnItsOwn()
	{
		// The submission path used to translate a reporter's typed choice.
		// It does not any more (ADR-0072), and Core holds no translator caller
		// at all — the only one is the administrator-gated endpoint above.
		Assembly.Load("HpacSafety.Core")
			.GetTypes()
			.SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Static
																	  | BindingFlags.Public | BindingFlags.NonPublic))
			.ShouldNotContain(method =>
				method.GetParameters().Any(parameter => parameter.ParameterType == typeof(ITranslator)));
	}

	[Then(@"it is told that translation is unavailable")]
	public void ThenTranslationIsUnavailable()
	{
		_available.ShouldBeFalse();
	}

	[Then(@"the answer carries no credential and no provider detail")]
	public async Task ThenTheAnswerCarriesNothingSensitive()
	{
		var cause = await Should.ThrowAsync<TranslationUnavailableException>(() =>
			_translator.Translate(["Were you injured?"], Locale.EnCa, Locale.FrCa, CancellationToken.None));

		cause.Message.ShouldNotContain("key");
		cause.Message.ShouldNotContain("Were you injured?");
	}

	[Then(@"no environment substitutes a stand-in that returns the text unchanged")]
	public void ThenNoEnvironmentSubstitutesAStandIn()
	{
		// Registration takes no environment switch, and the only adapter it
		// ever registers is the real provider. A stand-in that echoed its input
		// got stored as an answer's translation (ADR-0109).
		var register = typeof(TranslationServiceCollectionExtensions)
			.GetMethod(nameof(TranslationServiceCollectionExtensions.AddHpacSafetyTranslation));

		register!.GetParameters().ShouldNotContain(parameter => parameter.ParameterType == typeof(bool));

		typeof(ITranslator).Assembly.GetTypes()
			.Concat(typeof(TranslationServiceCollectionExtensions).Assembly.GetTypes())
			.Where(type => type is { IsClass: true, IsAbstract: false } && typeof(ITranslator).IsAssignableFrom(type))
			.Select(type => type.Name)
			.ShouldBe(["DeepLTranslator"]);
	}

	/// <summary>The web application's source, found from the test binary.</summary>
	private static string WebSourceRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);

		while (directory is not null
			   && !Directory.Exists(Path.Combine(directory.FullName, "src", "web", "src")))
		{
			directory = directory.Parent;
		}

		return Path.Combine(
			directory?.FullName ?? throw new DirectoryNotFoundException("Could not find src/web/src."),
			"src", "web", "src");
	}

	/// <summary>A translator that answers without a provider.</summary>
	private sealed class StubTranslator : ITranslator
	{
		public bool Configured { get; set; } = true;

		public bool IsConfigured => Configured;

		public Task<IReadOnlyList<string>> Translate(
			IReadOnlyList<string> texts,
			Locale source,
			Locale target,
			CancellationToken cancellationToken)
		{
			return Configured
				? Task.FromResult<IReadOnlyList<string>>([.. texts.Select(text => $"[{target.Code}] {text}")])
				: throw new TranslationUnavailableException("Translation is not configured on this server.");
		}
	}
}
