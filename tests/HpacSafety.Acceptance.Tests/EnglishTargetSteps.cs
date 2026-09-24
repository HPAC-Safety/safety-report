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
///     Which English DeepL is asked for (REQ-WLD-028, REQ-WLD-029). DeepL has no
///     Canadian English, so the target comes from <c>Translation:EnglishTarget</c>
///     and anything DeepL does not offer stops startup. Translation goes through
///     the same registration the API and the Worker use, with DeepL itself
///     replaced by a transport that records the request.
/// </summary>
[Binding]
[Scope(Feature = "Web, localization, and design")]
public sealed class EnglishTargetSteps : IDisposable
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private readonly RecordingTransport _transport = new();
	private string? _setting;
	private Exception? _startup;

	[Given(@"^the translation settings name (.+) as the English target$")]
	public void GivenTheEnglishTargetSetting(string setting)
	{
		_setting = setting == "nothing" ? null : setting;
	}

	[When(@"French text is sent to DeepL to be translated into English")]
	public async Task WhenFrenchIsTranslatedIntoEnglish()
	{
		await using var provider = Provider();
		var translator = provider.GetRequiredService<ITranslator>();
		await translator.Translate(["Synthétique : le pilote s'est posé."], Locale.FrCa, Locale.EnCa, CancellationToken.None);
	}

	[When(@"the translator's settings are validated at startup")]
	public async Task WhenValidatedAtStartup()
	{
		await using var provider = Provider();
		_startup = Record.Exception(() => provider.GetRequiredService<IStartupValidator>().Validate());
	}

	[Then(@"^the request asks DeepL for French to (EN-US|EN-GB)$")]
	public void ThenTheRequestAsksFor(string code)
	{
		using var sent = JsonDocument.Parse(_transport.Body!);
		sent.RootElement.GetProperty("source_lang").GetString().ShouldBe("FR");
		sent.RootElement.GetProperty("target_lang").GetString().ShouldBe(code);
	}

	[Then(@"startup fails, naming the Translation:EnglishTarget setting")]
	public void ThenStartupFails()
	{
		_startup.ShouldBeOfType<OptionsValidationException>().Message.ShouldContain("Translation:EnglishTarget");
	}

	public void Dispose()
	{
		_transport.Dispose();
	}

	private ServiceProvider Provider()
	{
		var settings = new Dictionary<string, string?> { ["Translation:ApiKey"] = "synthetic-key:fx" };

		if (_setting is not null)
		{
			settings["Translation:EnglishTarget"] = _setting;
		}

		var services = new ServiceCollection()
			.AddHpacSafetyTranslation(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
		services.AddSingleton<IHttpClientFactory>(new TransportFactory(_transport));
		return services.BuildServiceProvider();
	}

	private sealed class RecordingTransport : HttpMessageHandler
	{
		public string? Body { get; private set; }

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
																	 CancellationToken cancellationToken)
		{
			Body = await request.Content!.ReadAsStringAsync(cancellationToken);
			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("""{"translations":[{"text":"Synthetic: the pilot landed."}]}""", Encoding.UTF8, "application/json"),
			};
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
