using System.Text.RegularExpressions;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     The deterministic marking pass from
///     <see href="../../../docs/decisions/ADR-0082-a-deterministic-marking-pass-precedes-the-one-model-call.md">ADR-0082</see>.
///     Runs on an already-partitioned <see cref="SummarizationInput" /> and replaces any
///     exact or token-level occurrence of a private context value inside report content
///     with a marker naming the private question it came from. Private context is left
///     untouched and is still supplied to the model as recognition context for anything
///     this pass does not catch.
/// </summary>
public static class PrivateValueMarker
{
	/// <summary>
	///     Candidate tokens shorter than this are never matched on their own — too many
	///     unrelated short words would collide with them.
	/// </summary>
	public const int MinimumTokenLength = 4;

	/// <summary>
	///     Common words long enough to pass <see cref="MinimumTokenLength" /> but too
	///     generic to safely mark on their own when they happen to appear inside a
	///     multi-word private value (e.g. a compass direction inside a place name).
	///     Grown as false positives are found; not exhaustive by design.
	/// </summary>
	private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
	{
		"north", "south", "east", "west", "saint", "fort", "lake", "river", "city", "port",
		"nord", "sud", "est", "ouest", "sainte", "grande", "petit", "petite",
	};

	/// <summary>Applies the marking pass, returning a new input with report content marked.</summary>
	public static SummarizationInput Mark(SummarizationInput input)
	{
		ArgumentNullException.ThrowIfNull(input);

		var candidates = BuildCandidates(input.PrivateContext);
		if (candidates.Count == 0)
		{
			return input;
		}

		var pattern = BuildPattern(candidates);
		var markedReportContent = input.ReportContent
			.Select(field => field with { Value = pattern.Replace(field.Value, match => Marker(match, candidates)) })
			.ToList()
			.AsReadOnly();

		return SummarizationInput.WithReportContent(input, markedReportContent);
	}

	private static List<Candidate> BuildCandidates(IReadOnlyList<SummarizationField> privateContext)
	{
		var candidates = new List<Candidate>();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var field in privateContext)
		{
			var value = CollapseWhitespace(field.Value);
			if (value.Length == 0)
			{
				continue;
			}

			AddCandidate(candidates, seen, value, field.QuestionKey);

			var tokens = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (tokens.Length <= 1)
			{
				continue;
			}

			foreach (var token in tokens)
			{
				if (token.Length < MinimumTokenLength
					|| Stopwords.Contains(token))
				{
					continue;
				}

				AddCandidate(candidates, seen, token, field.QuestionKey);
			}
		}

		// Longest match first, so a whole-value candidate's pattern is offered to the
		// regex engine before its own tokens get a chance to fragment it.
		return [.. candidates.OrderByDescending(candidate => candidate.Value.Length)];
	}

	private static void AddCandidate(List<Candidate> candidates,
									 HashSet<string> seen,
									 string value,
									 string questionKey)
	{
		if (!seen.Add(value))
		{
			return;
		}

		candidates.Add(new Candidate(value, questionKey));
	}

	private static Regex BuildPattern(List<Candidate> candidates)
	{
		var alternation = string.Join(
			'|',
			candidates.Select((candidate,
							   index) => $"(?<c{index}>{EscapeWithFlexibleWhitespace(candidate.Value)})"));

		return new Regex($@"(?<![\p{{L}}\p{{N}}])(?:{alternation})(?![\p{{L}}\p{{N}}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
	}

	private static string EscapeWithFlexibleWhitespace(string value)
	{
		var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Regex.Escape);
		return string.Join(@"\s+", parts);
	}

	private static string Marker(Match match,
								 List<Candidate> candidates)
	{
		// The pattern is built from exactly these named groups, so one is always
		// the one that matched.
		var index = Enumerable.Range(0, candidates.Count).First(i => match.Groups[$"c{i}"].Success);
		return $"[PRIVATE:{candidates[index].QuestionKey}]";
	}

	private static string CollapseWhitespace(string value)
	{
		return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
	}

	private sealed record Candidate(string Value, string QuestionKey);
}
