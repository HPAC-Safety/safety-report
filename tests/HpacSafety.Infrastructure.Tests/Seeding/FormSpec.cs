using System.Text;
using System.Text.RegularExpressions;

namespace HpacSafety.Infrastructure.Tests.Seeding;

/// <summary>
/// Reads <c>docs/form-spec.md</c> — the generated description of the Typeform
/// question set — so a test can compare the seeded question bank against the
/// source of truth rather than against a second copy of it.
/// </summary>
internal static partial class FormSpec
{
    /// <summary>One field of the specification, in the order it appears.</summary>
    internal sealed record Field(string Label, string TypeCode, string? SectionLabel)
    {
        /// <summary>The description bullet under the field, if it has one.</summary>
        public string? Help { get; set; }

        /// <summary>The choice labels, in order. Empty unless the field has choices.</summary>
        public List<string> Choices { get; } = [];

        /// <summary>Whether the choices line says the field takes more than one.</summary>
        public bool IsMultiSelect { get; set; }
    }

    internal static IReadOnlyList<Field> Fields()
    {
        var fields = new List<Field>();
        string? section = null;
        Field? current = null;
        StringBuilder? detail = null;
        var detailIsChoices = false;

        void FlushDetail()
        {
            if (current is null || detail is null)
            {
                return;
            }

            var text = detail.ToString();
            if (detailIsChoices)
            {
                var match = ChoicesLine().Match(text);
                current.IsMultiSelect = match.Groups["multi"].Success;
                foreach (Match choice in Quoted().Matches(match.Groups["list"].Value))
                {
                    current.Choices.Add(choice.Groups[1].Value);
                }
            }
            else
            {
                current.Help = text;
            }

            detail = null;
        }

        foreach (var line in File.ReadAllLines(SpecPath()).SkipWhile(l => l != "## Fields").Skip(1))
        {
            var bullet = FieldBullet().Match(line);
            if (bullet.Success)
            {
                FlushDetail();
                var nested = bullet.Groups["indent"].Value.Length >= 2;
                current = new Field(bullet.Groups["label"].Value, bullet.Groups["type"].Value, nested ? section : null);
                fields.Add(current);

                if (!nested)
                {
                    section = current.TypeCode is "group" or "contact_info" ? current.Label : null;
                }

                continue;
            }

            var detailBullet = DetailBullet().Match(line);
            if (detailBullet.Success)
            {
                FlushDetail();
                var text = detailBullet.Groups["text"].Value;
                detailIsChoices = text.StartsWith("Choices", StringComparison.Ordinal);
                detail = new StringBuilder(text);
                continue;
            }

            if (line.Length == 0)
            {
                continue;
            }

            // A wrapped continuation of the bullet above, which Typeform writes
            // for anything with a line break in it.
            detail?.Append('\n').Append(line);
        }

        FlushDetail();
        return fields;
    }

    private static string SpecPath()
    {
        var local = Path.Combine(AppContext.BaseDirectory, "form-spec.md");
        return File.Exists(local)
            ? local
            : throw new FileNotFoundException(
                "form-spec.md was not copied next to the test assembly. See the None item in the test project.");
    }

    [GeneratedRegex(@"^(?<indent> *)- \*\*(?<label>.+?)\*\* — `(?<type>[a-z_]+)`$")]
    private static partial Regex FieldBullet();

    [GeneratedRegex(@"^ +- (?<text>.*)$")]
    private static partial Regex DetailBullet();

    [GeneratedRegex(@"^Choices(?<multi> \(multi-select\))?: (?<list>.*)$")]
    private static partial Regex ChoicesLine();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex Quoted();
}
