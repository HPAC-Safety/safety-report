namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     Features tagged <c>@xunit:collection(MeasuresAllocation)</c> run alone, after
///     everything that runs in parallel. REQ-MED-024 counts process-wide allocation
///     to prove a 50 MB attachment is never buffered whole, and another scenario
///     allocating alongside it would be counted too.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MeasuresAllocationRunsAlone
{
	/// <summary>The collection name the feature tag refers to.</summary>
	public const string Name = "MeasuresAllocation";
}
