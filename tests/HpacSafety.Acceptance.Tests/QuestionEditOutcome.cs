using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     What an Administrator's edit to a question left behind, shared between the
///     step classes that make an edit and <see cref="QuestionForkSteps" />, which
///     judges it.
/// </summary>
/// <remarks>
///     One edit is made against the domain (<see cref="ReporterAddedChoiceSteps" />)
///     and another through the booted API (<see cref="QuestionForkEndpointSteps" />),
///     but the feature file judges both with the same sentences. Reqnroll injects
///     one instance per scenario into every binding class that asks for it.
/// </remarks>
public sealed class QuestionEditOutcome
{
	/// <summary>The question the Administrator edited, as it stands afterwards.</summary>
	public Question? Original { get; set; }

	/// <summary>The question that is live after the edit: the original, revised, or its replacement.</summary>
	public Question? Live { get; set; }

	/// <summary>An answer given before the edit, when the scenario has one.</summary>
	public ReportAnswer? Answer { get; set; }

	/// <summary>The English wording the answer was given under.</summary>
	public string? OriginalLabelEn { get; set; }
}
