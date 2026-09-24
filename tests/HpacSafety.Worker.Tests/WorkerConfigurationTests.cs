using HpacSafety.Core;
using HpacSafety.Infrastructure.AiChatClient;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace HpacSafety.Worker.Tests;

/// <summary>
///     The committed Worker configuration names the summarization provider, model, and
///     reasoning level together, and never a key (ADR-0104).
/// </summary>
public sealed class WorkerConfigurationTests
{
	[Fact]
	public void GivenCommittedAppSettings_WhenBound_ThenGeminiFlashAtLowReasoningWithNoKey()
	{
		// Given
		var configuration = new ConfigurationBuilder()
			.AddJsonFile(CommittedAppSettings(), optional: false)
			.Build();

		// When
		var options = configuration.GetSection(AiChatClientOptions.SectionName).Get<AiChatClientOptions>();

		// Then
		options.ShouldNotBeNull();
		options.Provider.ShouldBe("Gemini");
		options.Model.ShouldBe("gemini-3.7-flash");
		options.ReasoningEffort.ShouldBe(ReasoningEffort.Low);
		options.ApiKey.ShouldBeNullOrEmpty();
	}

	/// <summary>The source file, not a copy in the output, so the test reads what is committed.</summary>
	private static string CommittedAppSettings()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HpacSafety.slnx")))
		{
			directory = directory.Parent;
		}

		directory.ShouldNotBeNull("the repository root holds HpacSafety.slnx");
		return Path.Combine(directory.FullName, "src", "HpacSafety.Worker", "appsettings.json");
	}
}
