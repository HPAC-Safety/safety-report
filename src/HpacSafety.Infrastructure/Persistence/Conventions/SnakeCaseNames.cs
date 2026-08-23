using System.Globalization;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace HpacSafety.Infrastructure.Persistence.Conventions;

/// <summary>
/// Names every column, key, index, and constraint in <c>snake_case</c>, which
/// is what PostgreSQL folds unquoted identifiers to anyway.
/// </summary>
/// <remarks>
/// Anything named explicitly in a configuration is left alone, so a column that
/// has to carry a particular name — <c>summaries.language</c>, for instance —
/// keeps it. Applied last, after every configuration has run. See ADR-0019.
/// </remarks>
public static class SnakeCaseNames
{
    /// <summary>Applies the naming to the whole model.</summary>
    /// <param name="modelBuilder">The model being built.</param>
    public static void Apply(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            if (entity.FindAnnotation(RelationalAnnotationNames.TableName) is null
                && entity.GetTableName() is { } table)
            {
                entity.SetTableName(ToSnakeCase(table));
            }

            foreach (var property in entity.GetProperties())
            {
                if (property.FindAnnotation(RelationalAnnotationNames.ColumnName) is null)
                {
                    property.SetColumnName(ToSnakeCase(property.Name));
                }
            }

            foreach (var key in entity.GetKeys())
            {
                key.SetName(ToSnakeCase(key.GetName() ?? string.Empty));
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                foreignKey.SetConstraintName(ToSnakeCase(foreignKey.GetConstraintName() ?? string.Empty));
            }

            foreach (var index in entity.GetIndexes())
            {
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName() ?? string.Empty));
            }
        }
    }

    /// <summary>Turns <c>ReportAnswerId</c> into <c>report_answer_id</c>.</summary>
    /// <param name="name">A PascalCase or already-snake_case name.</param>
    public static string ToSnakeCase(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var builder = new StringBuilder(name.Length + 8);

        for (var i = 0; i < name.Length; i++)
        {
            var character = name[i];

            if (char.IsUpper(character) && i > 0 && name[i - 1] != '_' && !char.IsUpper(name[i - 1]))
            {
                builder.Append('_');
            }

            builder.Append(char.ToLower(character, CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
