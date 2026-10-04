using System.Reflection;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     Guards the run-alone collections a feature asks for with
///     <c>@xunit:collection(Name)</c>.
/// </summary>
/// <remarks>
///     Reqnroll.xUnit.v3 turns the tag into a <c>Category</c> trait and no
///     <c>[Collection]</c>, so <c>XunitCollectionBindings.cs</c> adds the attribute by
///     hand. Without it the feature runs in parallel with the rest, which breaks the
///     allocation measurement (REQ-MED-024) and the shared question bank (ADR-0185)
///     only intermittently. This asserts every tagged feature class carries the
///     collection its tag names.
/// </remarks>
public sealed class XunitCollectionBindingTests
{
	private const string TagPrefix = "xunit:collection(";

	[Fact]
	public void GivenFeaturesTaggedWithAnXunitCollection_WhenTheirClassesAreInspected_ThenEachIsInTheNamedCollection()
	{
		var tagged = typeof(XunitCollectionBindingTests).Assembly.GetTypes()
			.Select(type => (Type: type, Names: CollectionNamesFromTags(type)))
			.Where(entry => entry.Names.Count > 0)
			.ToList();

		tagged.ShouldNotBeEmpty();
		foreach (var (type, names) in tagged)
		{
			var actual = type.GetCustomAttributes<CollectionAttribute>().Select(attribute => attribute.Name).ToList();

			actual.ShouldBe(names, $"{type.FullName} must be in the collection its @xunit:collection tag names");
		}
	}

	private static List<string> CollectionNamesFromTags(Type type)
	{
		return type.GetCustomAttributes<TraitAttribute>()
			.Where(trait => trait.Name == "Category" && trait.Value.StartsWith(TagPrefix, StringComparison.Ordinal))
			.Select(trait => trait.Value[TagPrefix.Length..^1])
			.ToList();
	}
}
