using System.Net;
using System.Text;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Infrastructure.Translation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     Gemini machine translation, en-CA and fr-CA (REQ-WLD-033 through REQ-WLD-041,
///     ADR-0179). Translation goes through the same registration the API and the
///     Worker use, with Gemini itself replaced by a transport that records the
///     request and answers with whatever the scenario says the model replied.
/// </summary>
[Binding]
[Scope(Feature = "Web, localization, and design")]
public sealed class GeminiTranslationSteps : IDisposable
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private const string SyntheticKey = "synthetic-gemini-key";
	private const string PlaceholderText = "Showing {count} reports in <b>bold</b>";

	private readonly Dictionary<string, string?> _settings = [];
	private readonly RecordingTransport _transport = new();
	private IReadOnlyList<string> _texts = ["Synthetic: the pilot landed."];
	private IReadOnlyList<string>? _result;
	private TranslationUnavailableException? _refusal;
	private OptionsValidationException? _startupFailure;
	private bool? _configured;
	private string _reply = "";

	[Given(@"^a Gemini key is configured$")]
	public void GivenAGeminiKeyIsConfigured()
	{
		_settings["AiChatClient:ApiKey"] = SyntheticKey;
		_settings["AiChatClient:Provider"] = "Gemini";
	}

	[Given(@"^the summary call's Gemini key is configured$")]
	public void GivenTheSummaryKeyIsConfigured()
	{
		GivenAGeminiKeyIsConfigured();
	}

	[Given(@"^no other key exists$")]
	public void GivenNoOtherKeyExists()
	{
		// The retired translation-only names configure nothing.
		_settings.Keys.ShouldAllBe(key => key.StartsWith("AiChatClient:", StringComparison.Ordinal));
	}

	[Given(@"^no Gemini key is configured, in Development or anywhere else$")]
	public void GivenNoGeminiKey()
	{
		_settings.Clear();
	}

	[Given(@"^the committed term list requires ""upload"" to be rendered ""téléverser"", never ""télécharg…""$")]
	public void GivenTheCommittedTermList()
	{
		var terms = File.ReadAllText(Path.Combine(RepositoryRoot(), "locales", "terms.json"));

		using var document = JsonDocument.Parse(terms);
		var upload = document.RootElement.GetProperty("upload");
		upload.GetProperty("fr-CA").GetString().ShouldBe("téléverser");
		upload.GetProperty("forbidden").EnumerateArray().Select(form => form.GetString()).ShouldContain("télécharg");
	}

	[Given(@"^three strings to translate$")]
	public void GivenThreeStrings()
	{
		_texts = ["First synthetic string.", "Second synthetic string.", "Third synthetic string."];
	}

	[Given(@"^the model replies with (one translation|three translations|an empty translation|a sentence instead of the JSON)$")]
	public void GivenTheModelReplies(string kind)
	{
		_reply = kind switch
		{
			"one translation" => """{"translations":["Un"]}""",
			"three translations" => """{"translations":["Un","Deux","Trois"]}""",
			"an empty translation" => """{"translations":["Un",""]}""",
			_ => "Happy to help! Un and Deux, as requested.",
		};
		_transport.Reply = _reply;
	}

	[Given(@"^a string with a \{count\} placeholder and a <b>bold</b> tag$")]
	public void GivenAStringWithPlaceholderAndTag()
	{
		_texts = [PlaceholderText];
	}

	[Given(@"^the Translation settings name (.+) as the model and (.+) as the reasoning effort$")]
	public void GivenTheTranslationSettings(string model,
											string effort)
	{
		Apply("Translation:Model", model);
		Apply("Translation:ReasoningEffort", effort);

		void Apply(string key,
				   string value)
		{
			switch (value)
			{
				case "nothing":
					break;
				case "blank":
					_settings[key] = "";
					break;
				default:
					_settings[key] = value;
					break;
			}
		}
	}

	[When(@"^French text is translated into English$")]
	public async Task WhenFrenchIsTranslatedIntoEnglish()
	{
		_texts = ["Synthétique : le pilote s'est posé."];
		await Translate(Locale.FrCa, Locale.EnCa);
	}

	[When(@"^English text is translated into French$")]
	public async Task WhenEnglishIsTranslatedIntoFrench()
	{
		await Translate(Locale.EnCa, Locale.FrCa);
	}

	[When(@"^text is translated in either direction$")]
	public async Task WhenTranslatedInEitherDirection()
	{
		await Translate(Locale.EnCa, Locale.FrCa);
		await Translate(Locale.FrCa, Locale.EnCa);
	}

	[When(@"^text is translated$")]
	public async Task WhenTextIsTranslated()
	{
		await Translate(Locale.EnCa, Locale.FrCa);
	}

	[When(@"^they are translated$")]
	public async Task WhenTheyAreTranslated()
	{
		await Translate(Locale.EnCa, Locale.FrCa);
	}

	[When(@"^two strings are translated$")]
	public async Task WhenTwoStringsAreTranslated()
	{
		_texts = ["First synthetic string.", "Second synthetic string."];
		await Translate(Locale.EnCa, Locale.FrCa);
	}

	[When(@"^the model replies with a translation that (keeps both|renames the placeholder|drops the bold tag)$")]
	public async Task WhenTheModelRepliesWithATranslation(string change)
	{
		_transport.Reply = JsonSerializer.Serialize(new
		{
			translations = new[]
			{
				change switch
				{
					"keeps both" => "Affichage de {count} signalements en <b>gras</b>",
					"renames the placeholder" => "Affichage de {compte} signalements en <b>gras</b>",
					_ => "Affichage de {count} signalements en gras",
				},
			},
		});
		_reply = _transport.Reply;

		await Translate(Locale.EnCa, Locale.FrCa);
	}

	[When(@"^the host starts$")]
	public async Task WhenTheHostStarts()
	{
		await using var provider = Provider();

		try
		{
			provider.GetRequiredService<IStartupValidator>().Validate();
		}
		catch (OptionsValidationException failure)
		{
			_startupFailure = failure;
			return;
		}

		await Translate(provider, Locale.EnCa, Locale.FrCa);
	}

	[When(@"^the API or the Worker translates text$")]
	public async Task WhenTheApiOrTheWorkerTranslates()
	{
		// One registration serves both hosts, so one call shows what either sends.
		await Translate(Locale.EnCa, Locale.FrCa);
	}

	[Then(@"^the prompt asks for Canadian English, with Canadian spelling$")]
	public void ThenThePromptAsksForCanadianEnglish()
	{
		Prompt().ShouldContain("into Canadian English (en-CA), with Canadian spelling");
	}

	[Then(@"^the request names no American or British English$")]
	public void ThenNoAmericanOrBritishEnglish()
	{
		var body = _transport.Body!;

		body.ShouldNotContain("EN-US");
		body.ShouldNotContain("EN-GB");
		body.ShouldNotContain("American");
		body.ShouldNotContain("British");
	}

	[Then(@"^the prompt asks for Canadian French$")]
	public void ThenThePromptAsksForCanadianFrench()
	{
		Prompt().ShouldContain("into Canadian French (fr-CA)");
	}

	[Then(@"^the prompt instructs the model to render ""upload"" as ""téléverser"" and never ""télécharg…""$")]
	public void ThenThePromptCarriesTheTerm()
	{
		_transport.Bodies.Count.ShouldBe(2);

		foreach (var body in _transport.Bodies)
		{
			var prompt = PromptOf(body);
			prompt.ShouldContain("\"upload\"");
			prompt.ShouldContain("\"téléverser\"");
			prompt.ShouldContain("Never \"télécharg…\"");
		}
	}

	[Then(@"^translation is refused rather than echoed back$")]
	public void ThenTranslationIsRefused()
	{
		_refusal.ShouldNotBeNull();
		_result.ShouldBeNull();
	}

	[Then(@"^no request is sent to any provider$")]
	public void ThenNoRequestIsSent()
	{
		_transport.Bodies.ShouldBeEmpty();
	}

	[Then(@"^the translator reports itself unconfigured$")]
	public void ThenTheTranslatorIsUnconfigured()
	{
		_configured.ShouldBe(false);
	}

	[Then(@"^one request is sent, and its only report-derived content is those three strings$")]
	public void ThenOneRequestCarriesOnlyTheStrings()
	{
		_transport.Bodies.Count.ShouldBe(1);

		using var body = JsonDocument.Parse(_transport.Body!);
		var messages = body.RootElement.GetProperty("messages").EnumerateArray().ToList();
		messages.Count.ShouldBe(2);

		using var user = JsonDocument.Parse(messages[1].GetProperty("content").GetString()!);
		user.RootElement.EnumerateObject().Select(property => property.Name).ShouldBe(["texts"]);
		user.RootElement.GetProperty("texts").EnumerateArray().Select(text => text.GetString()).ShouldBe(_texts);

		// The system prompt is the versioned file and the term list, never a string of the report.
		var system = messages[0].GetProperty("content").GetString().ShouldNotBeNull();

		foreach (var text in _texts)
		{
			system.ShouldNotContain(text);
		}
	}

	[Then(@"^the translations come back in the order given, one for each string$")]
	public void ThenTheyComeBackInOrder()
	{
		_result.ShouldNotBeNull();
		_result.ShouldBe(_texts.Select(text => $"Traduit : {text}").ToList());
	}

	[Then(@"^translation is refused$")]
	public void ThenRefused()
	{
		_refusal.ShouldNotBeNull();
	}

	[Then(@"^the failure carries nothing the model replied with$")]
	public void ThenTheFailureQuotesNothing()
	{
		_refusal.ShouldNotBeNull();
		_refusal.Message.ShouldNotContain("Happy to help");
		_refusal.Message.ShouldNotContain("Un and Deux");
		_refusal.Message.ShouldNotContain("Deux");
	}

	[Then(@"^the result is the translation, with both intact$")]
	public void ThenTheResultIsTheTranslation()
	{
		_refusal.ShouldBeNull();
		_result.ShouldBe(["Affichage de {count} signalements en <b>gras</b>"]);
	}

	[Then(@"^the result is a refusal that carries no reply text$")]
	public void ThenTheResultIsARefusal()
	{
		_refusal.ShouldNotBeNull();
		_refusal.Message.ShouldNotContain("Affichage");
		_refusal.Message.ShouldNotContain("signalements");
	}

	[Then(@"^translation asks for (\S+) at (\w+) reasoning$")]
	public void ThenTranslationAsksFor(string model,
									   string effort)
	{
		_startupFailure.ShouldBeNull();

		using var body = JsonDocument.Parse(_transport.Body!);
		body.RootElement.GetProperty("model").GetString().ShouldBe(model);
		body.RootElement.GetProperty("reasoning_effort").GetString().ShouldBe(effort);
	}

	[Then(@"^startup fails, naming the (Translation:\w+) setting$")]
	public void ThenStartupFails(string setting)
	{
		_startupFailure.ShouldNotBeNull().Message.ShouldContain(setting);
		_transport.Bodies.ShouldBeEmpty();
	}

	[Then(@"^the request is authorized with that key$")]
	public void ThenTheRequestIsAuthorizedWithThatKey()
	{
		_transport.Authorization.ShouldBe($"Bearer {SyntheticKey}");
	}

	public void Dispose()
	{
		_transport.Dispose();
	}

	private static string RepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);

		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
		{
			directory = directory.Parent;
		}

		return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find the repository root.");
	}

	private string Prompt()
	{
		return PromptOf(_transport.Body!);
	}

	private static string PromptOf(string body)
	{
		using var sent = JsonDocument.Parse(body);
		return sent.RootElement.GetProperty("messages")[0].GetProperty("content").GetString()!;
	}

	private async Task Translate(Locale source,
								 Locale target)
	{
		await using var provider = Provider();
		await Translate(provider, source, target);
	}

	private async Task Translate(ServiceProvider provider,
								 Locale source,
								 Locale target)
	{
		var translator = provider.GetRequiredService<ITranslator>();
		_configured = translator.IsConfigured;

		try
		{
			_result = await translator.Translate(_texts, source, target, CancellationToken.None);
			_refusal = null;
		}
		catch (TranslationUnavailableException refusal)
		{
			_refusal = refusal;
			_result = null;
		}
	}

	private ServiceProvider Provider()
	{
		var services = new ServiceCollection()
			.AddHpacSafetyTranslation(new ConfigurationBuilder().AddInMemoryCollection(_settings).Build());
		services.AddSingleton<IHttpClientFactory>(new TransportFactory(_transport));

		return services.BuildServiceProvider();
	}

	private sealed class RecordingTransport : HttpMessageHandler
	{
		public List<string> Bodies { get; } = [];

		public string? Body => Bodies.Count > 0 ? Bodies[^1] : null;

		public string? Authorization { get; private set; }

		/// <summary>What the model says. Left empty, it translates each string it is sent.</summary>
		public string? Reply { get; set; }

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
																	 CancellationToken cancellationToken)
		{
			var body = await request.Content!.ReadAsStringAsync(cancellationToken);
			Bodies.Add(body);
			Authorization = request.Headers.Authorization?.ToString();

			var content = Reply ?? Echo(body);
			var completion = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content } } } });

			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(completion, Encoding.UTF8, "application/json"),
			};
		}

		private static string Echo(string body)
		{
			using var sent = JsonDocument.Parse(body);
			var user = sent.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!;
			using var texts = JsonDocument.Parse(user);

			return JsonSerializer.Serialize(new
			{
				translations = texts.RootElement.GetProperty("texts").EnumerateArray()
					.Select(text => $"Traduit : {text.GetString()}")
					.ToList(),
			});
		}
	}

	private sealed class TransportFactory(HttpMessageHandler transport) : IHttpClientFactory
	{
		public HttpClient CreateClient(string name)
		{
			return new HttpClient(transport, disposeHandler: false);
		}
	}
}
